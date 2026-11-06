using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private MapWarpDocument _mapWarpDocument;
        private string _mapWarpError, _mapWarpUiKey, _mapWarpName = "", _mapWarpSelectedId;
        private List<MapWarpPoint> _mapWarpRows = new List<MapWarpPoint>();
        private string _mapWarpNavigationKey, _mapWarpActiveId;
        private Transform _mapWarpNavigationRoot;
        private string MapWarpPath => Path.Combine(_pluginDir, "MapWarpPoints.json");

        private void LoadMapWarpPoints()
        {
            _mapWarpDocument = null; _mapWarpUiKey = null; _mapWarpSelectedId = null; _mapWarpName = "";
            ResetMapWarpNavigation();
            try { _mapWarpDocument = MapWarpPointStore.Load(MapWarpPath); _mapWarpError = null; }
            catch (Exception ex) { _mapWarpError = "登録地点を読み込めません。元ファイルは保持しています：" + ex.Message; }
        }

        private bool TryMapWarpContext(out string key, out Transform root, out bool hotel)
        {
            key = null; root = null; hotel = false;
            if (!_sessionStarted || !CanUseRoomOverlay() || _roomOverlayLoading || _femaleChara == null) return false;
            if (string.IsNullOrEmpty(_roomOverlaySceneName)) return TrySourceMapWarpContext(out key, out root);
            hotel = _roomOverlaySceneName == VamHotelSceneName;
            root = hotel ? (_vamHotelPivot != null ? _vamHotelPivot.transform : null) :
                (_roomOverlayPositionRoot != null ? _roomOverlayPositionRoot.transform : null);
            if (root == null) return false;
            if (hotel) key = "special:hotel";
            else if (IsAiIslandScene(_roomOverlaySceneName)) key = "special:ai_shoujo_main";
            else if (_roomOverlaySceneName == StreetlightRoadSceneKey) key = "special:streetlight-road";
            else if (IsSandboxMapScene(_roomOverlaySceneName)) key = "special:sandbox-map";
            else if (!string.IsNullOrEmpty(_roomOverlayBundleName)) key = "bundle:" + _roomOverlayBundleName.Replace('\\', '/') + "\nscene:" + _roomOverlaySceneName;
            return key != null && (hotel || !string.IsNullOrEmpty(_roomOverlayPositionKey));
        }

        private void RefreshMapWarpRows(string key)
        {
            _mapWarpUiKey = key;
            _mapWarpRows = _mapWarpDocument == null ? new List<MapWarpPoint>() :
                _mapWarpDocument.Points.Where(p => p.MapKey == key).ToList();
        }

        private bool _mapWarpAddOpen, _mapWarpEditOpen;

        private void DrawMapWarpPoints()
        {
            if (!TryMapWarpContext(out string key, out Transform root, out _)) return;
            PrepareMapWarpNavigation(key, root);
            if (_mapWarpUiKey != key)
            {
                _mapWarpSelectedId = null; _mapWarpName = "";
                _mapWarpAddOpen = false; _mapWarpEditOpen = false; RefreshMapWarpRows(key);
            }
            GUILayout.BeginVertical("box");
            GUILayout.Label("登録地点 ― このマップ専用");
            if (_mapWarpError != null) GUILayout.Label(_mapWarpError);
            if (_mapWarpDocument != null)
            {
                if (_mapWarpRows.Count == 0) GUILayout.Label("未登録です。下の「地点を追加」から登録してください。");
                foreach (MapWarpPoint point in _mapWarpRows)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button((_mapWarpSelectedId == point.Id ? "● " : "") + point.Name, GUILayout.Width(200f)))
                    { _mapWarpSelectedId = point.Id; _mapWarpName = point.Name; }
                    if (GUILayout.Button("ワープ", GUILayout.Width(64f))) WarpToMapPoint(key, point.Id);
                    GUILayout.EndHorizontal();
                }
                GUILayout.Label("上部の◁▷で登録地点を切替。□は保存済みの「元の場所」へ戻ります。");
            }
            GUILayout.EndVertical();
            if (_mapWarpDocument == null) return;

            GUILayout.BeginVertical("box");
            _mapWarpAddOpen = GUILayout.Toggle(_mapWarpAddOpen, "地点を追加", GUILayout.ExpandWidth(false));
            if (_mapWarpAddOpen || _mapWarpRows.Count == 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("地点名", GUILayout.Width(60f));
                _mapWarpName = GUILayout.TextField(_mapWarpName, 80, GUILayout.Width(200f));
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("現在地を登録", GUILayout.Width(112f))) RegisterMapWarpPoint(key, false, null);
                if (GUILayout.Button("注視点を登録", GUILayout.Width(112f))) RegisterMapWarpPoint(key, true, null);
                GUILayout.EndHorizontal();
                GUILayout.Label("現在地＝キャラ位置 ／ 注視点＝カメラが向く基準点");
            }
            GUILayout.EndVertical();

            var selected = _mapWarpRows.SingleOrDefault(p => p.Id == _mapWarpSelectedId);
            if (selected == null) return;
            if (selected.IsOrigin)
            {
                GUILayout.BeginVertical("box");
                GUILayout.Label("元の場所（初期配置の記録）");
                GUILayout.Label("最初の配置を保存済みです。マップを変更してもゲームを終了しても残ります。");
                GUILayout.Label("元の場所の配置は変更できません。編集できるのはスクショだけです。");
                DrawMapWarpOriginPhoto(selected);
                if (GUILayout.Button("スクショを撮り直す", GUILayout.ExpandWidth(false))) QueueHPlaceUi(() => SaveMapWarpOriginPhoto(key));
                GUILayout.EndVertical();
                return;
            }
            GUILayout.BeginVertical("box");
            _mapWarpEditOpen = GUILayout.Toggle(_mapWarpEditOpen, "選択地点の設定：「" + selected.Name + "」", GUILayout.ExpandWidth(false));
            if (_mapWarpEditOpen)
            {
                GUILayout.BeginHorizontal();
                _mapWarpName = GUILayout.TextField(_mapWarpName, 80, GUILayout.Width(200f));
                if (GUILayout.Button("名前を変更", GUILayout.Width(96f))) ChangeMapWarpPoint(key, selected.Id, false);
                GUILayout.EndHorizontal();
                GUILayout.Label("回転中心：" + (selected.HasRotationCenter ? "個別指定" : "登録地点と同じ"));
                if (_mapWarpActiveId != selected.Id) GUILayout.Label("この地点へワープすると、この中心を使います。");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("登録地点に戻す", GUILayout.Width(120f))) SetMapPointRotationCenter(key, selected.Id, null);
                if (GUILayout.Button("現在地を中心", GUILayout.Width(112f))) SetMapPointRotationCenter(key, selected.Id, false);
                if (GUILayout.Button("注視点を中心", GUILayout.Width(112f))) SetMapPointRotationCenter(key, selected.Id, true);
                GUILayout.EndHorizontal();
                GUILayout.Label("中心だけ保存。移動先と他の地点は変わりません。");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("現在地で上書き", GUILayout.Width(120f))) RegisterMapWarpPoint(key, false, selected.Id);
                if (GUILayout.Button("注視点で上書き", GUILayout.Width(120f))) RegisterMapWarpPoint(key, true, selected.Id);
                if (GUILayout.Button("削除", GUILayout.Width(52f))) ChangeMapWarpPoint(key, selected.Id, true);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }
        private void SetMapPointRotationCenter(string key, string id, bool? viewed)
        {
            if (!MapWarpActionAllowed(key, out Transform root, out _)) return;
            try
            {
                var document = _mapWarpDocument.Clone();
                var point = document.Points.SingleOrDefault(p => p.MapKey == key && p.Id == id);
                if (point == null) return;
                if (point.IsOrigin) throw new InvalidOperationException("元の場所の回転中心は変更できません。");
                Vector3 world = _femaleChara.transform.position;
                if (viewed == true)
                {
                    var scene = CanonPlugins.Shared.PluginSessionGate.HScene;
                    var camera = scene != null && scene.flags != null ? scene.flags.ctrlCamera : null;
                    if (camera == null) throw new InvalidOperationException("注視点を取得できません。");
                    Vector3 focus = camera.GetCameraData().Pos;
                    world = camera.transBase != null ? camera.transBase.TransformPoint(focus) : focus;
                }
                Vector3 local = viewed.HasValue ? root.InverseTransformPoint(world) : Vector3.zero;
                point.HasRotationCenter = viewed.HasValue;
                point.CenterX = local.x; point.CenterY = local.y; point.CenterZ = local.z;
                CommitMapWarpDocument(document, key, "「" + point.Name + "」の回転中心を保存しました。マップ位置は変えていません。");
            }
            catch (Exception ex) { _mapWarpError = "回転中心を保存できません：" + ex.Message; }
        }

        private bool MapWarpActionAllowed(string expected, out Transform root, out bool hotel)
        {
            bool valid = TryMapWarpContext(out string key, out root, out hotel) && key == expected && _mapWarpDocument != null;
            return valid;
        }

        private void CommitMapWarpDocument(MapWarpDocument document, string key, string message)
        {
            MapWarpPointStore.Save(MapWarpPath, document);
            _mapWarpDocument = document; RefreshMapWarpRows(key); _mapWarpError = message;
        }

        private void RegisterMapWarpPoint(string key, bool viewed, string replaceId)
        {
            if (!MapWarpActionAllowed(key, out Transform root, out bool hotel)) return;
            try
            {
                string name = (_mapWarpName ?? "").Trim();
                if (name.Length == 0) throw new InvalidOperationException("地点名を入力してください。");
                Vector3 world = _femaleChara.transform.position;
                if (viewed)
                {
                    var h = CanonPlugins.Shared.PluginSessionGate.HScene;
                    var camera = h != null && h.flags != null ? h.flags.ctrlCamera : null;
                    if (camera == null) throw new InvalidOperationException("注視点を取得できません。");
                    Vector3 focus = camera.GetCameraData().Pos;
                    world = camera.transBase != null ? camera.transBase.TransformPoint(focus) : focus;
                }
                Vector3 local = root.InverseTransformPoint(world);
                RoomOverlayPosition settings = hotel ? null : EnsureRoomOverlayPositionConfig(_roomOverlayPositionKey);
                var point = new MapWarpPoint { Id = replaceId ?? Guid.NewGuid().ToString("N"), MapKey = key, Name = name,
                    X = local.x, Y = local.y, Z = local.z,
                    RotationX = hotel ? _vamHotelRotationX.Value : settings.RotationX.Value,
                    RotationY = hotel ? _vamHotelRotationY.Value : settings.RotationY.Value,
                    RotationZ = hotel ? _vamHotelRotationZ.Value : settings.RotationZ.Value,
                    Scale = hotel ? _vamHotelScale.Value : 1f };
                var document = _mapWarpDocument.Clone();
                if (replaceId != null)
                {
                    int index = document.Points.FindIndex(p => p.MapKey == key && p.Id == replaceId);
                    if (index < 0) throw new InvalidOperationException("選択地点がありません。");
                    var old = document.Points[index];
                    if (old.IsOrigin) throw new InvalidOperationException("元の場所の配置は変更できません。");
                    point.HasRotationCenter = old.HasRotationCenter;
                    point.CenterX = old.CenterX; point.CenterY = old.CenterY; point.CenterZ = old.CenterZ;
                    document.Points[index] = point;
                }
                else document.Points.Add(point);
                CommitMapWarpDocument(document, key, "地点を保存しました。"); _mapWarpSelectedId = point.Id;
            }
            catch (Exception ex) { _mapWarpError = "地点を保存できません：" + ex.Message; }
        }

        private void ChangeMapWarpPoint(string key, string id, bool delete)
        {
            if (!MapWarpActionAllowed(key, out _, out _)) return;
            try
            {
                var document = _mapWarpDocument.Clone();
                var point = document.Points.SingleOrDefault(p => p.MapKey == key && p.Id == id);
                if (point == null) return;
                if (point.IsOrigin) throw new InvalidOperationException("元の場所は削除・改名できません。");
                if (delete) document.Points.Remove(point);
                else point.Name = (_mapWarpName ?? "").Trim();
                CommitMapWarpDocument(document, key, delete ? "地点を削除しました。" : "地点名を変更しました。");
                if (delete && _mapWarpSelectedId == id) _mapWarpSelectedId = null;
                if (delete && _mapWarpActiveId == id) _mapWarpActiveId = null;
            }
            catch (Exception ex) { _mapWarpError = "地点を変更できません：" + ex.Message; }
        }

        private void WarpToMapPoint(string key, string id)
        {
            _hDefaultRequest.Cancel();++_roomRestoreGeneration;
            if (!MapWarpActionAllowed(key, out Transform root, out bool hotel)) return;
            var point = _mapWarpDocument.Points.SingleOrDefault(p => p.MapKey == key && p.Id == id);
            if (point == null) return;
            PrepareMapWarpNavigation(key, root);
            if (point.IsOrigin) { ApplyMapWarpOrigin(key, point, hotel); return; }
            ConfigEntry<float>[] entries = null; float[] previous = null;
            bool autoSave = Config.SaveOnConfigSet;
            try
            {
                point.Validate();
                RoomOverlayPosition settings = hotel ? null : EnsureRoomOverlayPositionConfig(_roomOverlayPositionKey);
                Quaternion rotation = root.rotation;
                Vector3 scale = root.lossyScale;
                Vector3 target = MapWarpGeometry.RootForPoint(new Vector3(point.X, point.Y, point.Z), _femaleChara.transform.position, rotation, scale);
                Vector3 offset = hotel ? target - _vamHotelActorAnchor : MapWarpGeometry.OverlayOffset(target, _roomOverlayBasePosition,
                    _roomOverlayBaseRotation, rotation, _roomOverlayActorAnchor, settings.RotateAroundActor.Value);
                if (!IsFiniteMapVector(offset)) throw new InvalidOperationException("移動後の座標が不正です。");
                entries = hotel ? new[] { _vamHotelOffsetX, _vamHotelOffsetY, _vamHotelOffsetZ, _vamHotelRotationX, _vamHotelRotationY, _vamHotelRotationZ, _vamHotelScale } :
                    new[] { settings.X, settings.Y, settings.Z, settings.RotationX, settings.RotationY, settings.RotationZ };
                previous = entries.Select(e => e.Value).ToArray();
                Config.SaveOnConfigSet = false;
                // A landmark selects a location, while the current pose determines the map orientation.
                float[] values = (float[])previous.Clone();
                values[0] = offset.x; values[1] = offset.y; values[2] = offset.z;
                for (int i = 0; i < entries.Length; i++) entries[i].Value = values[i];
                if (hotel) ApplyVamHotelTransform(); else ApplyRoomOverlayPosition();
                Config.Save();
                _mapWarpActiveId = id;
                _mapWarpSelectedId = id; _mapWarpName = point.Name;
                _mapTabInputKey = null; EnsureMapTabInputs(hotel ? "hotel" : _roomOverlayPositionKey, entries);
                _mapWarpError = "「" + point.Name + "」へ移動しました。カメラと体位は変更していません。";
            }
            catch (Exception ex)
            {
                if (previous != null)
                {
                    for (int i = 0; i < entries.Length; i++) entries[i].Value = previous[i];
                    if (hotel) ApplyVamHotelTransform(); else ApplyRoomOverlayPosition();
                }
                _mapWarpError = "ワープできません：" + ex.Message;
            }
            finally { Config.SaveOnConfigSet = autoSave; }
        }

        private void ResetMapWarpNavigation()
        {
            _mapWarpNavigationKey = null; _mapWarpNavigationRoot = null;
            _mapWarpActiveId = null;
        }

        private void PrepareMapWarpNavigation(string key, Transform root)
        {
            if (_mapWarpNavigationKey == key && _mapWarpNavigationRoot == root) return;
            ResetMapWarpNavigation();
            _mapWarpNavigationKey = key; _mapWarpNavigationRoot = root;
            _mapWarpSelectedId = null; _mapWarpName = "";
            try
            {
                if (_mapWarpDocument != null && TryMapWarpContext(out string current, out _, out bool hotel) && current == key)
                {
                    var created = MapWarpOrigin.CreateIfMissing(_mapWarpDocument, key, () => CaptureMapWarpOrigin(root, hotel));
                    if (created != null) CommitMapWarpDocument(created, key, "元の場所を初期配置として保存しました。");
                    var origin = MapWarpOrigin.Find(_mapWarpDocument, key);
                    if (origin != null && origin.Placement.SequenceEqual(MapWarpPlacementEntries(hotel).Select(e => e.Value))
                        && (hotel || origin.PlacementActorCenter == EnsureRoomOverlayPositionConfig(_roomOverlayPositionKey).RotateAroundActor.Value))
                    { _mapWarpActiveId = _mapWarpSelectedId = origin.Id; _mapWarpName = origin.Name; }
                }
            }
            catch (Exception ex) { _mapWarpError = "元の場所を保存できません：" + ex.Message; }
            RefreshMapWarpRows(key);
        }

        private ConfigEntry<float>[] MapWarpPlacementEntries(bool hotel)
        {
            if (hotel) return new[] { _vamHotelOffsetX, _vamHotelOffsetY, _vamHotelOffsetZ,
                _vamHotelRotationX, _vamHotelRotationY, _vamHotelRotationZ, _vamHotelScale };
            var settings = EnsureRoomOverlayPositionConfig(_roomOverlayPositionKey);
            return new[] { settings.X, settings.Y, settings.Z, settings.RotationX, settings.RotationY, settings.RotationZ };
        }

        private void ApplyMapTabRotation(bool hotel)
        {
            if (_mapWarpActiveId == null || !TryMapWarpContext(out string key, out Transform root, out bool currentHotel) ||
                hotel != currentHotel || key != _mapWarpNavigationKey || root != _mapWarpNavigationRoot)
            {
                ApplyMapTabTransform(hotel);
                return;
            }
            var point = _mapWarpDocument?.Points.SingleOrDefault(p => p.MapKey == key && p.Id == _mapWarpActiveId);
            if (point == null) return;
            // The saved center belongs to the map, independently of the placement and actor.
            Vector3 local = point.HasRotationCenter ? new Vector3(point.CenterX, point.CenterY, point.CenterZ)
                : new Vector3(point.X, point.Y, point.Z);
            Vector3 center = root.TransformPoint(local);
            RoomOverlayPosition settings = hotel ? null : EnsureRoomOverlayPositionConfig(_roomOverlayPositionKey);
            Quaternion rotation = hotel
                ? Quaternion.Euler(_vamHotelRotationX.Value, _vamHotelRotationY.Value, _vamHotelRotationZ.Value)
                : _roomOverlayBaseRotation * Quaternion.Euler(settings.RotationX.Value, settings.RotationY.Value, settings.RotationZ.Value);
            Vector3 target = MapWarpGeometry.RootForPoint(local, center, rotation, root.lossyScale);
            Vector3 offset = hotel ? target - _vamHotelActorAnchor : MapWarpGeometry.OverlayOffset(target,
                _roomOverlayBasePosition, _roomOverlayBaseRotation, rotation, _roomOverlayActorAnchor, settings.RotateAroundActor.Value);
            if (!IsFiniteMapVector(offset)) return;
            ConfigEntry<float>[] entries = MapWarpPlacementEntries(hotel);
            bool autoSave = Config.SaveOnConfigSet;
            try
            {
                Config.SaveOnConfigSet = false;
                entries[0].Value = offset.x; entries[1].Value = offset.y; entries[2].Value = offset.z;
                if (hotel) ApplyVamHotelTransform(); else ApplyRoomOverlayPosition();
                SyncMapTabPositionInputs(entries);
            }
            finally { Config.SaveOnConfigSet = autoSave; }
        }

        private void StepMapWarpPoint(int direction)
        {
            _hDefaultRequest.Cancel();++_roomRestoreGeneration;
            if (!TryMapWarpContext(out string key, out Transform root, out _) || _mapWarpDocument == null) return;
            if (_hPlaceDraft != null || _hPlaceBusy) { _mapWarpError = "H場所の編集・撮影が終わってから切り替えてください。"; return; }
            PrepareMapWarpNavigation(key, root);
            string id = MapLocationNavigation.Next(_mapWarpDocument, _hLocations, key, _hPlaceActive?.Id ?? _mapWarpActiveId, direction);
            if (id == null) return;
            if (_hLocations != null && _hLocations.Presets.Any(p => HLocationMapIdentity.Same(p.MapKey,key) && p.Id == id)) RecallHPlace(id, false);
            else
            {
                if (_hPlaceOriginal != null)
                {
                    var camera = ReadHPlaceCamera();
                    StopHLocations(resetBaseline: false);
                    WriteHPlaceCamera(camera);
                }
                WarpToMapPoint(key, id);
            }
        }

        private void ReturnMapWarpOrigin()
        {
            _hDefaultRequest.Cancel();++_roomRestoreGeneration;
            if (_hPlaceDraft != null || _hPlaceBusy) return;
            if (!TryMapWarpContext(out string key, out Transform root, out bool hotel)) return;
            PrepareMapWarpNavigation(key, root);
            var origin = MapWarpOrigin.Find(_mapWarpDocument, key);
            if (origin == null) return;
            if (_hPlaceOriginal != null)
            {
                var camera = ReadHPlaceCamera();
                StopHLocations(resetBaseline: false);
                WriteHPlaceCamera(camera);
            }
            ApplyMapWarpOrigin(key, origin, hotel);
        }

        private void ApplyMapWarpOrigin(string key, MapWarpPoint origin, bool hotel)
        {
            ConfigEntry<float>[] entries = MapWarpPlacementEntries(hotel);
            float[] previous = entries.Select(e => e.Value).ToArray();
            var settings = hotel ? null : EnsureRoomOverlayPositionConfig(_roomOverlayPositionKey);
            bool previousCenter = settings != null && settings.RotateAroundActor.Value;
            bool autoSave = Config.SaveOnConfigSet;
            try
            {
                origin.Validate();
                if (origin.Placement.Length != entries.Length) throw new InvalidOperationException("元の場所の配置項目が一致しません。");
                Config.SaveOnConfigSet = false;
                for (int i = 0; i < entries.Length; i++) entries[i].Value = origin.Placement[i];
                if (settings != null) settings.RotateAroundActor.Value = origin.PlacementActorCenter;
                if (hotel) ApplyVamHotelTransform(); else ApplyRoomOverlayPosition();
                Config.Save();
                _mapTabInputKey = null; EnsureMapTabInputs(hotel ? "hotel" : _roomOverlayPositionKey, entries);
                _mapWarpActiveId = _mapWarpSelectedId = origin.Id; _mapWarpName = origin.Name;
                _mapWarpError = "保存済みの元の場所へ戻しました。";
            }
            catch (Exception ex)
            {
                for (int i = 0; i < entries.Length; i++) entries[i].Value = previous[i];
                if (settings != null) settings.RotateAroundActor.Value = previousCenter;
                if (hotel) ApplyVamHotelTransform(); else ApplyRoomOverlayPosition();
                _mapWarpError = "元の場所へ戻せません：" + ex.Message;
            }
            finally { Config.SaveOnConfigSet = autoSave; }
        }

        private MapWarpPoint CaptureMapWarpOrigin(Transform root, bool hotel)
        {
            Vector3 local = root.InverseTransformPoint(_femaleChara.transform.position);
            float[] placement = MapWarpPlacementEntries(hotel).Select(e => e.Value).ToArray();
            return new MapWarpPoint { X = local.x, Y = local.y, Z = local.z,
                RotationX = placement[3], RotationY = placement[4], RotationZ = placement[5],
                Scale = hotel ? placement[6] : 1f, Placement = placement,
                PlacementActorCenter = !hotel && EnsureRoomOverlayPositionConfig(_roomOverlayPositionKey).RotateAroundActor.Value };
        }

    }
}
