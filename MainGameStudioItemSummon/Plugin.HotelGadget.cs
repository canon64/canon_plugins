using System;
using System.Collections.Generic;
using System.Linq;
using CanonPlugins.Shared;
using UnityEngine;
namespace MainGameStudioItemSummon
{
    public sealed partial class Plugin
    {
        [Serializable] private sealed class Source { public string bundle, asset, manifest, name; public bool active; }
        [Serializable] private sealed class Sources { public Source[] items; public string[] itemRecords; }
        public bool HotelGadgetAllowed => SessionGate.Allowed && isActiveAndEnabled;
        public void SetHotelGadgetVisible(bool visible)
        {
            if (!HotelGadgetAllowed) throw new InvalidOperationException("アイテム呼出はこのHでは許可されていません。");
            if (visible) { foreach (var item in _summoned) if (item != null) item.SetActive(true); return; }
            // The hotel host retains the saved sources/poses while hidden. Keeping inactive
            // objects in _summoned makes Capture report visible=true and prevents recall.
            foreach (var item in _summoned) if (item != null) { item.SetActive(false); Destroy(item); }
            _summoned.Clear(); _summonedSources.Clear();
        }
        public string CaptureHotelGadget(Transform room)
        {
            var items = _summoned.Where(o => o != null).ToArray();
            var data = GadgetRoomSnapshot.Capture(room, items.Select(o => o.transform).ToArray());
            data.visible = items.Length > 0;
            var sources = items.Select(o => {
                if (!_summonedSources.TryGetValue(o, out var source)) throw new InvalidOperationException("呼出アイテムの資産情報がありません。");
                return new Source { bundle = source.BundlePath, asset = source.FileName, manifest = source.Manifest, name = source.Name, active = o.activeSelf };
            }).ToArray();
            data.runtime = JsonUtility.ToJson(new Sources { items = sources, itemRecords = sources.Select(source => JsonUtility.ToJson(source)).ToArray() });
            return data.Encode();
        }
        public void RestoreHotelGadget(Transform room, string json)
        {
            if (!HotelGadgetAllowed) throw new InvalidOperationException("アイテム呼出はこのHでは許可されていません。");
            var data = GadgetRoomSnapshot.Decode(json);
            var sources = string.IsNullOrEmpty(data.runtime) ? null : JsonUtility.FromJson<Sources>(data.runtime);
            if (sources?.itemRecords != null) sources.items = sources.itemRecords.Select(item => JsonUtility.FromJson<Source>(item)).ToArray();
            if (data.visible && (sources?.items == null || sources.items.Length != data.poses.Length)) throw new ArgumentException("呼出アイテムの保存数が不正です。");
            var created = new List<GameObject>();
            try
            {
                if (data.visible)
                    for (int i = 0; i < sources.items.Length; ++i)
                    {
                        var source = sources.items[i];
                        if (source == null || string.IsNullOrWhiteSpace(source.bundle) || string.IsNullOrWhiteSpace(source.asset)) throw new ArgumentException("呼出アイテムの資産情報が空です。");
                        var item = CommonLib.LoadAsset<GameObject>(source.bundle, source.asset, true, source.manifest);
                        if (item == null) throw new InvalidOperationException("保存したアイテムを読み込めません：" + source.name);
                        created.Add(item); RemoveColliders(item); item.name = "Summoned_" + source.name;
                        data.poses[i].Apply(room, item.transform); item.SetActive(source.active);
                    }
                foreach (var old in _summoned) if (old != null) { old.SetActive(false); Destroy(old); }
                _summoned.Clear(); _summonedSources.Clear();
                for (int i = 0; i < created.Count; ++i)
                {
                    var source = sources.items[i]; var item = created[i]; _summoned.Add(item);
                    _summonedSources[item] = new CatalogEntry { BundlePath = source.bundle, FileName = source.asset, Manifest = source.manifest, Name = source.name };
                }
            }
            catch { foreach (var item in created) if (item != null) Destroy(item); throw; }
        }
        public bool VerifyHotelGadgetState(Transform room, string json)
        {
            var data = GadgetRoomSnapshot.Decode(json);
            var saved = string.IsNullOrEmpty(data.runtime) ? null : JsonUtility.FromJson<Sources>(data.runtime);
            if (saved?.itemRecords != null) saved.items = saved.itemRecords.Select(item => JsonUtility.FromJson<Source>(item)).ToArray();
            var actual = _summoned.Where(item => item != null).ToArray();
            if (!data.visible) return actual.Length == 0;
            if (saved?.items == null || saved.items.Length != actual.Length) return false;
            for (int i = 0; i < actual.Length; ++i)
            {
                var expected = saved.items[i];
                if (expected == null || !_summonedSources.TryGetValue(actual[i], out var source) || source.BundlePath != expected.bundle ||
                    source.FileName != expected.asset || source.Manifest != expected.manifest || source.Name != expected.name || actual[i].activeSelf != expected.active) return false;
            }
            return true;
        }
    }
}
