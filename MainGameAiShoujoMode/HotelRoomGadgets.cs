using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using CanonPlugins.Shared;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    internal sealed class HotelRoomGadgets
    {
        private sealed class Client
        {
            internal string guid, label, error;
            internal BaseUnityPlugin plugin;
            internal MethodInfo capture, restore, set, verifyPlacement, verifyState;
            internal PropertyInfo allowed;
            internal bool visible, restoreFailed;
        }
        private readonly Client[] _clients;
        private static Client[] KnownClients() => new[] {
            new Client { guid="com.kks.maingame.vrdildo",label="ディルド" },
            new Client { guid="com.kks.maingame.vrneckchain",label="鎖" },
            new Client { guid="com.kks.maingame.iphonecamera",label="iPhone" },
            new Client { guid="com.kks.maingame.syringe",label="注射器" },
            new Client { guid="com.kks.maingame.magicpaint",label="マジック・スプレー" },
            new Client { guid="com.kks.maingame.candle",label="ロウソク" },
            new Client { guid="com.kks.maingame.desktopdisplay",label="ディスプレイ" },
            new Client { guid="com.kks.maingame.vrhandcuffs",label="手錠" },
            new Client { guid="com.kks.main.gameobjectcomposer",label="Composer（全オブジェクト）" },
            new Client { guid="com.kks.main.girlbodyikgizmo",label="HipHijack（客室姿勢）" }
        };
        private readonly Dictionary<string, string> _states;
        private readonly Transform _room;
        private readonly Action<HotelRoomGadget[]> _save;
        private readonly Func<HotelRoomSaveBatch> _batchFactory;
        private readonly Action<HotelRoomSaveBatch> _report;
        private readonly Action<HotelRoomGadget> _persistOne;
        internal string Status { get; private set; }
        internal HotelRoomGadgets(Transform room, HotelRoomGadget[] saved, Action<HotelRoomGadget[]> save,
            Func<HotelRoomSaveBatch> batchFactory = null, Action<HotelRoomSaveBatch> report = null, Action<HotelRoomGadget> persistOne = null,
            HotelRoomRestoreBatch restoration = null)
        {
            _room = room; _save = save;
            _batchFactory = batchFactory; _report = report;
            _persistOne = persistOne;
            _states = new Dictionary<string, string>();
            foreach (var record in saved ?? Array.Empty<HotelRoomGadget>())
                try { if (record == null || string.IsNullOrWhiteSpace(record.guid) || string.IsNullOrWhiteSpace(record.payload)) throw new ArgumentException("アイテムの保存キーまたは内容が欠損しています。"); _states.Add(record.guid, record.payload); }
                catch (Exception ex) { restoration?.Failure("小物保存データ", "gadgets", ex); }
            // Discover providers once at room entry, including future item plugins.
            var clients = KnownClients().ToList();
            foreach (var pair in Chainloader.PluginInfos)
                if (pair.Value.Instance != null && !clients.Any(c => c.guid == pair.Key)
                    && pair.Value.Instance.GetType().GetMethod("CaptureHotelGadget", new[] { typeof(Transform) }) != null)
                    clients.Add(new Client { guid = pair.Key, label = pair.Key });
            _clients = clients.OrderBy(c => c.guid == "com.kks.main.girlbodyikgizmo").ToArray();
            foreach (string guid in _states.Keys.Where(guid => !_clients.Any(c => c.guid == guid)))
                restoration?.Failure(guid, "gadgets[guid=" + guid + "]", new InvalidOperationException("保存されたproviderが未導入です。"));
            foreach (var client in _clients)
            {
                if (!Chainloader.PluginInfos.TryGetValue(client.guid, out var info) || info.Instance == null)
                {
                    client.error = "未導入";
                    if (_states.ContainsKey(client.guid)) restoration?.Failure(client.label, "gadgets[guid=" + client.guid + "]", new InvalidOperationException("保存されたproviderが未導入です。"));
                    continue;
                }
                client.plugin = info.Instance;
                var type = client.plugin.GetType();
                client.capture = type.GetMethod("CaptureHotelGadget"); client.restore = type.GetMethod("RestoreHotelGadget");
                client.set = type.GetMethod("SetHotelGadgetVisible"); client.allowed = type.GetProperty("HotelGadgetAllowed");
                client.verifyPlacement = type.GetMethod("VerifyHotelGadgetPlacement", new[] { typeof(Transform), typeof(string) });
                client.verifyState = type.GetMethod("VerifyHotelGadgetState", new[] { typeof(Transform), typeof(string) });
                if (client.capture == null || client.restore == null || client.set == null || client.allowed == null)
                { client.error = "対応DLLへ更新が必要"; restoration?.Failure(client.label, "gadgets[guid=" + client.guid + "]", new InvalidOperationException(client.error)); continue; }
                if (restoration != null)
                {
                    if (_states.TryGetValue(client.guid, out var payload))
                    {
                        string actual = null;
                        bool restored = restoration.Item(client.label, "gadgets[guid=" + client.guid + "]", client.guid, () =>
                        {
                            if (!(bool)client.allowed.GetValue(client.plugin, null)) throw new InvalidOperationException("providerが無効または未許可です。");
                            GadgetRoomSnapshot.Decode(payload); return payload;
                        }, value => client.restore.Invoke(client.plugin, new object[] { _room, value }), value =>
                        {
                            actual = (string)client.capture.Invoke(client.plugin, new object[] { _room });
                            bool syringe = client.guid == "com.kks.maingame.syringe";
                            bool aligned = client.verifyPlacement != null;
                            if (aligned && !(bool)client.verifyPlacement.Invoke(client.plugin, new object[] { _room, value })) return false;
                            bool stateVerified = client.verifyState != null;
                            if (stateVerified && !(bool)client.verifyState.Invoke(client.plugin, new object[] { _room, value })) return false;
                            return EquivalentGadget(value, actual, syringe, aligned, stateVerified);
                        }, GadgetSummary, () => GadgetSummary(actual), () =>
                        {
                            string before = (string)client.capture.Invoke(client.plugin, new object[] { _room });
                            return () => client.restore.Invoke(client.plugin, new object[] { _room, before });
                        });
                        if (!restored) { client.restoreFailed = true; client.error = "復元・照合に失敗。共通通知を確認してください。"; }
                        else { client.visible = GadgetRoomSnapshot.Decode(actual).visible; client.error = null; }
                    }
                    else restoration.Observe("not-saved", () => "item=" + client.label + " key=gadgets[guid=" + client.guid + "] result=not-applicable");
                    continue;
                }
                try
                {
                    if (!(bool)client.allowed.GetValue(client.plugin, null)) { client.error = "無効または未許可"; continue; }
                    if (_states.TryGetValue(client.guid, out var json)) client.restore.Invoke(client.plugin, new object[] { _room, json });
                    Refresh(client);
                }
                catch (Exception ex) { client.error = ex.GetBaseException().Message; client.restoreFailed = true; }
            }
        }
        private static bool EquivalentGadget(string saved, string actual, bool legacyNeedlePose = false, bool alignedNeedle = false, bool stateVerified = false)
        {
            var expected = GadgetRoomSnapshot.Decode(saved); var observed = GadgetRoomSnapshot.Decode(actual);
            // Provider snapshots are canonical protocol records. Compare only persisted, supported fields.
            return expected.visible == observed.visible && (!expected.visible || expected.poses.Length == observed.poses.Length)
                && (!expected.visible || HotelGadgetComparison.Equivalent(expected.Encode(), observed.Encode(),
                    (left, right) => new Vector3((float)left, default(float), default(float)) == new Vector3((float)right, default(float), default(float)), legacyNeedlePose, alignedNeedle, stateVerified));
        }
        private static string GadgetSummary(string json)
        {
            var value = GadgetRoomSnapshot.Decode(json);
            return "visible=" + value.visible + " poses=[" + string.Join(";", value.poses.Select(p =>
                "position=" + p.GetType().GetField("position")?.GetValue(p) + " rotation=" + p.GetType().GetField("rotation")?.GetValue(p)
                + " scale=" + p.GetType().GetField("scale")?.GetValue(p))) + "] payloadChars=" + json.Length;
        }
        private void Refresh(Client client)
        {
            if (client.plugin == null || client.capture == null || client.allowed == null) return;
            if (client.restoreFailed) throw new InvalidOperationException("保存状態を復元できなかったため、元データを保持しています：" + client.error);
            if (!(bool)client.allowed.GetValue(client.plugin, null)) { client.error = "無効または未許可"; return; }
            string json = (string)client.capture.Invoke(client.plugin, new object[] { _room });
            var data = GadgetRoomSnapshot.Decode(json); client.visible = data.visible; client.error = null;
            // Keep the last placement when a hidden gadget has no transform to capture.
            if (!data.visible && _states.TryGetValue(client.guid, out var old))
            { var prior = GadgetRoomSnapshot.Decode(old); prior.visible = false; json = prior.Encode(); }
            _states[client.guid] = json;
        }
        internal void Refresh()
        {
            foreach (var client in _clients)
                try { Refresh(client); } catch (Exception ex) { client.error = ex.GetBaseException().Message; }
        }
        internal bool Save()
        {
            var batch = _batchFactory?.Invoke() ?? new HotelRoomSaveBatch(null, null, null, "gadgets-button");
            Save(batch, _persistOne ?? (value => _save(_states.Where(pair => pair.Key != value.guid)
                .Select(pair => new HotelRoomGadget { guid = pair.Key, payload = pair.Value }).Concat(new[] { value }).ToArray())));
            batch.Finish();
            Status = batch.Status;
            _report?.Invoke(batch);
            return batch.Succeeded;
        }
        internal void Save(HotelRoomSaveBatch batch, Action<HotelRoomGadget> persist)
        {
            foreach (var client in _clients)
            {
                string key = "gadgets[guid=" + client.guid + "]";
                if (client.plugin == null)
                {
                    batch.Skip(client.label, key, client.error ?? "provider-unavailable"); continue;
                }
                batch.Item(client.label, key, () =>
                {
                    if (client.capture == null || client.restore == null || client.set == null || client.allowed == null)
                        throw new InvalidOperationException("導入済みproviderの保存APIがありません。対応DLLへ更新が必要です。");
                    if (client.restoreFailed) throw new InvalidOperationException("復元失敗のため旧値を保持：" + client.error);
                    if (!(bool)client.allowed.GetValue(client.plugin, null)) throw new InvalidOperationException("無効または未許可");
                    string json;
                    try { json = (string)client.capture.Invoke(client.plugin, new object[] { _room }); }
                    catch (Exception ex) { throw new InvalidOperationException("provider-capture: " + ex.GetBaseException().Message); }
                    GadgetRoomSnapshot data;
                    try { data = GadgetRoomSnapshot.Decode(json); }
                    catch (Exception ex) { throw new InvalidOperationException("provider-payload-decode chars=" + (json?.Length.ToString() ?? "null") + ": " + ex.GetBaseException().Message); }
                    if (!data.visible && _states.TryGetValue(client.guid, out var old))
                    {
                        GadgetRoomSnapshot prior;
                        try { prior = GadgetRoomSnapshot.Decode(old); }
                        catch (Exception ex) { throw new InvalidOperationException("previous-payload-decode chars=" + (old?.Length.ToString() ?? "null") + ": " + ex.GetBaseException().Message); }
                        prior.visible = false; json = prior.Encode();
                    }
                    return new HotelRoomGadget { guid = client.guid, payload = json };
                }, value =>
                {
                    persist(value);
                }, value => "provider=" + value.guid + " payloadChars=" + value.payload.Length, value =>
                {
                    _states[client.guid] = value.payload;
                    client.error = null;
                });
            }
        }
        internal void Draw()
        {
            GUILayout.BeginVertical(GUI.skin.box); GUILayout.Label("ガジェット");
            foreach (var client in _clients)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(client.label + "：" + (client.error ?? (client.visible ? "使用中" : "非表示")), GUILayout.ExpandWidth(false));
                bool enabled = GUI.enabled; GUI.enabled = enabled && client.error == null;
                if (GUILayout.Button(client.visible ? "片付ける" : "呼び出す", GUILayout.ExpandWidth(false)))
                {
                    try
                    {
                        Refresh(client); bool show = !client.visible;
                        if (show && _states.TryGetValue(client.guid, out var old) && GadgetRoomSnapshot.Decode(old).poses.Any())
                        { var data = GadgetRoomSnapshot.Decode(old); data.visible = true; client.restore.Invoke(client.plugin, new object[] { _room, data.Encode() }); }
                        else client.set.Invoke(client.plugin, new object[] { show });
                        Refresh(client); Save();
                    }
                    catch (Exception ex) { client.error = ex.GetBaseException().Message; }
                }
                GUI.enabled = enabled; GUILayout.EndHorizontal();
            }
            if (GUILayout.Button("ガジェットの現在配置を保存", GUILayout.ExpandWidth(false))) Save();
            if (!string.IsNullOrEmpty(Status)) GUILayout.Label(Status);
            GUILayout.EndVertical();
        }
    }
}
