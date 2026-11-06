using System;
using System.Linq;
using CanonPlugins.Shared;
using UnityEngine;
using SessionGate = CanonPlugins.Local.SessionGate;
namespace MainGameObjectComposer
{
    public sealed partial class Plugin
    {
        [Serializable] private sealed class HotelLayout { public string layout, preset; }
        public bool HotelGadgetAllowed => SessionGate.Allowed && isActiveAndEnabled;
        public void SetHotelGadgetVisible(bool visible)
        {
            if (!HotelGadgetAllowed) throw new InvalidOperationException("ComposerはこのHでは許可されていません。");
            if (visible) RebuildAllRuntimeObjects(); else { DeactivatePreset(); DestroyAllRuntimeObjects(); _objects.Clear(); }
        }
        public string CaptureHotelGadget(Transform room)
        {
            if (!HotelGadgetAllowed) throw new InvalidOperationException("ComposerはこのHでは許可されていません。");
            var data = GadgetRoomSnapshot.Capture(room, _runtime.Root == null ? null : _runtime.Root.transform);
            data.runtime = JsonUtility.ToJson(new HotelLayout { layout = SerializeCurrentLayoutSnapshot(),
                preset = _activePreset == null ? null : JsonUtility.ToJson(_activePreset) });
            return data.Encode();
        }
        public void RestoreHotelGadget(Transform room, string json)
        {
            if (!HotelGadgetAllowed) throw new InvalidOperationException("ComposerはこのHでは許可されていません。");
            var data = GadgetRoomSnapshot.Decode(json);
            if (data.visible && data.poses.Length != 1) throw new ArgumentException("Composerの配置データが不正です。");
            var saved = JsonUtility.FromJson<HotelLayout>(data.runtime);
            if (saved == null || string.IsNullOrWhiteSpace(saved.layout)) throw new ArgumentException("Composerの保存配置が欠損しています。");
            var layout = JsonUtility.FromJson<ObjectLayoutFile>(saved.layout);
            if (layout == null || layout.objectsJson == null) throw new ArgumentException("Composerの保存配置が不正です。");
            foreach (string item in layout.objectsJson)
                if (string.IsNullOrWhiteSpace(item) || JsonUtility.FromJson<ManagedObjectData>(item) == null)
                    throw new ArgumentException("Composerの保存オブジェクトが不正です。");
            var preset = string.IsNullOrEmpty(saved.preset) ? null : JsonUtility.FromJson<PresetEntry>(saved.preset);
            DeactivatePreset();
            if (!data.visible) { DestroyAllRuntimeObjects(); ApplyLayoutData(layout, false); return; }
            ApplyLayoutData(layout, true);
            if (_runtime.Root == null) throw new InvalidOperationException("Composerを生成できません。");
            data.poses.Single().Apply(room, _runtime.Root.transform);
            if (preset != null)
            {
                _activePreset = preset;
                RebuildActivePresetCache(); ActivateHipHijackIksForCurrentLinks();
            }
            // Prevent the initial auto-selection from immediately replacing the saved layout.
            _lastAutoSwitchKey = BuildCurrentPositionKey();
            RaiseManagedObjectListChanged();
        }
    }
}
