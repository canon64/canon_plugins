using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Video;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(BaseMap), "LoadMapInfo")]
        private static void BaseMap_LoadMapInfo_Postfix(ref Dictionary<int, MapInfo.Param> __result)
        {
            try
            {
                Instance?.InjectCustomMapInfo(__result);
            }
            catch (Exception ex)
            {
                Instance?.LogError($"LoadMapInfo postfix failed: {ex}");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BaseMap), "LoadMapThumbnailInfo")]
        private static void BaseMap_LoadMapThumbnailInfo_Postfix(ref Dictionary<int, MapThumbnailInfo.Param> __result)
        {
            try
            {
                Instance?.InjectCustomThumbnailInfo(__result);
            }
            catch (Exception ex)
            {
                Instance?.LogError($"LoadMapThumbnailInfo postfix failed: {ex}");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BaseMap), "Reserve")]
        private static void BaseMap_Reserve_Postfix(BaseMap __instance)
        {
            try
            {
                Instance?.TryBlankifyCurrentMap(__instance);
            }
            catch (Exception ex)
            {
                Instance?.LogError($"Reserve postfix failed: {ex}");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MapSelectMenuScene), "Create")]
        private static void MapSelectMenuScene_Create_Postfix(
            MapSelectMenuScene.VisibleType visibleType,
            MapSelectMenuScene.ResultType resultType,
            IReadOnlyCollection<MapInfo.Param> infos,
            IReadOnlyDictionary<int, MapThumbnailInfo.Param> mapThumbnailInfoTable)
        {
            try
            {
                Instance?.DiagnoseMapVisibility(visibleType, resultType, infos, mapThumbnailInfoTable);
            }
            catch (Exception ex)
            {
                Instance?.LogError($"Create postfix diagnose failed: {ex}");
            }
        }

        private void InjectCustomMapInfo(Dictionary<int, MapInfo.Param> mapInfo)
        {
            if (mapInfo == null)
            {
                LogWarn("mapInfo dictionary is null");
                return;
            }

            if (mapInfo.Count == 0)
            {
                LogWarn("mapInfo dictionary is empty");
                return;
            }

            if (!mapInfo.TryGetValue(_settings.SourceMapNo, out var source) || source == null)
            {
                foreach (var kv in mapInfo)
                {
                    source = kv.Value;
                    break;
                }
                LogWarn($"source map {_settings.SourceMapNo} not found, fallback to first");
            }

            if (source == null)
            {
                LogWarn("source map is null, skip inject");
                return;
            }

            // Store source thumbnail id for thumbnail table injection.
            _sourceThumbnailId = source.ThumbnailMorningID != -1 ? source.ThumbnailMorningID
                : source.ThumbnailDayTimeID != -1 ? source.ThumbnailDayTimeID
                : source.ThumbnailEveningID != -1 ? source.ThumbnailEveningID
                : source.ThumbnailNightID;

            MapInfo.Param added = CloneMapParam(source);
            added.No = _settings.AddedMapNo;
            added.MapName = string.IsNullOrWhiteSpace(_settings.AddedMapName)
                ? $"blank_test_{_settings.AddedMapNo}"
                : _settings.AddedMapName.Trim();
            added.DisplayName = string.IsNullOrWhiteSpace(_settings.AddedDisplayName)
                ? $"Blank {_settings.AddedMapNo}"
                : _settings.AddedDisplayName.Trim();
            added.Sort = _settings.AddedSort;
            added.isGate = _settings.ForceIsGate;
            added.isFreeH = _settings.ForceIsFreeH;
            added.isH = _settings.ForceIsH;

            // Use plugin-injected thumbnail id for this added map.
            added.ThumbnailMorningID = _settings.AddedThumbnailID;
            added.ThumbnailDayTimeID = -1;
            added.ThumbnailEveningID = -1;
            added.ThumbnailNightID   = -1;

            mapInfo[_settings.AddedMapNo] = added;
            if (_settings.VerboseLog)
            {
                LogInfo(
                    $"map injected no={added.No} name={added.MapName} display={added.DisplayName} " +
                    $"assetBundle={added.AssetBundleName} asset={added.AssetName} " +
                    $"isGate={added.isGate} isFreeH={added.isFreeH} isH={added.isH} " +
                    $"thumbs=[{added.ThumbnailMorningID},{added.ThumbnailDayTimeID},{added.ThumbnailEveningID},{added.ThumbnailNightID}]");
            }
        }

        private void InjectCustomThumbnailInfo(Dictionary<int, MapThumbnailInfo.Param> table)
        {
            if (table == null)
            {
                LogWarn("thumbnail table is null, skip inject");
                return;
            }

            if (table.ContainsKey(_settings.AddedThumbnailID))
            {
                LogWarn($"thumbnail id={_settings.AddedThumbnailID} already exists, skip inject");
                return;
            }

            // Clone source thumbnail entry (bundle/asset) to custom id.
            MapThumbnailInfo.Param sourceThumb = null;
            if (_sourceThumbnailId != -1)
                table.TryGetValue(_sourceThumbnailId, out sourceThumb);
            if (sourceThumb == null)
                sourceThumb = table.Values.FirstOrDefault();

            if (sourceThumb == null)
            {
                LogWarn("thumbnail table has no entries, cannot inject custom thumbnail");
                return;
            }

            string name = string.IsNullOrWhiteSpace(_settings.AddedDisplayName)
                ? $"Blank {_settings.AddedMapNo}"
                : _settings.AddedDisplayName.Trim();

            var added = new MapThumbnailInfo.Param
            {
                ID     = _settings.AddedThumbnailID,
                Name   = name,
                Bundle = sourceThumb.Bundle,
                Asset  = sourceThumb.Asset,
            };

            table[_settings.AddedThumbnailID] = added;
            LogInfo(
                $"thumbnail injected id={added.ID} name={added.Name} " +
                $"bundle={added.Bundle} asset={added.Asset} " +
                $"(cloned from sourceThumbnailId={_sourceThumbnailId})");
        }

        private void DiagnoseMapVisibility(
            MapSelectMenuScene.VisibleType visibleType,
            MapSelectMenuScene.ResultType resultType,
            IReadOnlyCollection<MapInfo.Param> infos,
            IReadOnlyDictionary<int, MapThumbnailInfo.Param> mapThumbnailInfoTable)
        {
            if (infos == null)
            {
                LogWarn($"map ui diagnose: infos null visibleType={visibleType} resultType={resultType}");
                return;
            }

            MapInfo.Param added = infos.FirstOrDefault(x => x != null && x.No == _settings.AddedMapNo);
            if (added == null)
            {
                LogWarn($"map ui diagnose: added map no={_settings.AddedMapNo} not found in infos (count={infos.Count})");
                return;
            }

            bool routeFlagPass = added.isGate;
            bool freeHFlagPass = added.isFreeH;

            int? thumbId = added.FindThumbnailID();
            bool thumbExists = thumbId.HasValue &&
                mapThumbnailInfoTable != null &&
                mapThumbnailInfoTable.ContainsKey(thumbId.Value);

            // Whether our own thumbnail id exists in the table.
            bool ownThumbInjected = mapThumbnailInfoTable != null &&
                mapThumbnailInfoTable.ContainsKey(_settings.AddedThumbnailID);

            LogInfo(
                $"map ui diagnose: visibleType={visibleType} resultType={resultType} " +
                $"no={added.No} sort={added.Sort} " +
                $"isGate={added.isGate} isFreeH={added.isFreeH} isH={added.isH} " +
                $"thumbId={thumbId?.ToString() ?? "null"} thumbExists={thumbExists} " +
                $"ownThumbId={_settings.AddedThumbnailID} ownThumbInjected={ownThumbInjected} " +
                $"ThumbnailMorningID={added.ThumbnailMorningID}");

            if (!thumbExists)
            {
                int thumbCount = mapThumbnailInfoTable != null ? mapThumbnailInfoTable.Count : 0;
                LogWarn($"map ui diagnose: thumbnail missing for no={added.No} thumbId={thumbId?.ToString() ?? "null"} tableCount={thumbCount}");
            }
            if (!ownThumbInjected)
            {
                LogWarn($"map ui diagnose: own thumbnail not injected (id={_settings.AddedThumbnailID})");
            }
        }

        private void TryBlankifyCurrentMap(BaseMap map)
        {
            _lastReservedMap = map;

            if (map == null || map.mapRoot == null)
            {
                DestroyVideoRoom();
                _lastBlankifiedRootId = int.MinValue;
                return;
            }

            if (map.no != _settings.AddedMapNo)
            {
                DestroyVideoRoom();
                _lastBlankifiedRootId = int.MinValue;
                return;
            }

            if (_settings.BlankifySceneOnLoad)
            {
                int rootId = map.mapRoot.GetInstanceID();
                if (_lastBlankifiedRootId != rootId)
                {
                    _lastBlankifiedRootId = rootId;

                    int rendererCount = 0;
                    int terrainCount = 0;
                    int lightCount = 0;
                    int particleCount = 0;

                    if (_settings.DisableRenderers)
                    {
                        var renderers = map.mapRoot.GetComponentsInChildren<Renderer>(true);
                        for (int i = 0; i < renderers.Length; i++)
                        {
                            if (renderers[i] == null || !renderers[i].enabled) continue;
                            renderers[i].enabled = false;
                            rendererCount++;
                        }
                    }

                    if (_settings.DisableTerrains)
                    {
                        var terrains = map.mapRoot.GetComponentsInChildren<Terrain>(true);
                        for (int i = 0; i < terrains.Length; i++)
                        {
                            if (terrains[i] == null) continue;
                            terrains[i].drawHeightmap = false;
                            terrains[i].drawTreesAndFoliage = false;
                            terrainCount++;
                        }
                    }

                    if (_settings.DisableLights)
                    {
                        var lights = map.mapRoot.GetComponentsInChildren<Light>(true);
                        for (int i = 0; i < lights.Length; i++)
                        {
                            if (lights[i] == null || !lights[i].enabled) continue;
                            lights[i].enabled = false;
                            lightCount++;
                        }
                    }

                    if (_settings.DisableParticles)
                    {
                        var particles = map.mapRoot.GetComponentsInChildren<ParticleSystem>(true);
                        for (int i = 0; i < particles.Length; i++)
                        {
                            if (particles[i] == null) continue;
                            var emission = particles[i].emission;
                            if (!emission.enabled) continue;
                            emission.enabled = false;
                            particleCount++;
                        }
                    }

                    int audioCount = 0;
                    if (_settings.DisableAudioSources)
                    {
                        var audioSources = map.mapRoot.GetComponentsInChildren<AudioSource>(true);
                        for (int i = 0; i < audioSources.Length; i++)
                        {
                            if (audioSources[i] == null || audioSources[i].mute) continue;
                            audioSources[i].mute = true;
                            audioCount++;
                        }
                    }

                    LogInfo(
                        $"blankify no={map.no} root={map.mapRoot.name} " +
                        $"renderers={rendererCount} terrains={terrainCount} lights={lightCount} " +
                        $"particles={particleCount} audio={audioCount}");
                }
            }

            if (_settings.EnableVideoRoom)
            {
                EnsureVideoRoom(map);
            }
            else
            {
                DestroyVideoRoom();
            }
        }

        private void ReloadSettingsAndApply()
        {
            _settings = SettingsStore.LoadOrCreate(_pluginDir, LogInfo, LogWarn, LogError);
            SyncConfigEntriesFromSettings();

            if (_lastReservedMap == null || _lastReservedMap.mapRoot == null)
            {
                LogInfo("settings reloaded (map not ready)");
                return;
            }

            // Rebuild room to apply settings immediately.
            DestroyVideoRoom();
            _lastBlankifiedRootId = int.MinValue;
            TryBlankifyCurrentMap(_lastReservedMap);
            LogInfo(
                $"settings reloaded + applied mapNo={_lastReservedMap.no} " +
                $"useSphere={_settings.UseSphere} reverb={_settings.EnableVoiceReverb}");
        }
        private static MapInfo.Param CloneMapParam(MapInfo.Param src)
        {
            var dst = new MapInfo.Param
            {
                MapName = src.MapName,
                DisplayName = src.DisplayName,
                No = src.No,
                Sort = src.Sort,
                AssetBundleName = src.AssetBundleName,
                AssetName = src.AssetName,
                isGate = src.isGate,
                is2D = src.is2D,
                isWarning = src.isWarning,
                State = src.State,
                LookFor = src.LookFor,
                isOutdoors = src.isOutdoors,
                isFreeH = src.isFreeH,
                isSpH = src.isSpH,
                isSky = src.isSky,
                isH = src.isH,
                ThumbnailMorningID = src.ThumbnailMorningID,
                ThumbnailDayTimeID = src.ThumbnailDayTimeID,
                ThumbnailEveningID = src.ThumbnailEveningID,
                ThumbnailNightID = src.ThumbnailNightID
            };
            return dst;
        }
    }
}
