using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    internal sealed partial class HotelModeSession
    {
        private HotelRoomRestoreBatch _roomRestore;
        private static void PrepareRoomPoseProviders(HSceneProc scene)
        {
            // StartH can replace lstUseAnimInfo after the provider's gate-start callback.
            // Ask the provider to populate the actual final list before exact matching.
            if (!BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("com.kks.maingame.aibuposeclones", out var provider)) return;
            var instance = provider.Instance;
            var prepare = instance?.GetType().GetMethod("PrepareHotelRoomPoseCatalog", new[] { typeof(HSceneProc) });
            if (prepare == null) throw new MissingMethodException("クローン体位プラグインにホテル復元用APIがありません。");
            try { prepare.Invoke(instance, new object[] { scene }); }
            catch (System.Reflection.TargetInvocationException ex) { throw new InvalidOperationException("クローン体位一覧の準備に失敗しました。", ex.InnerException ?? ex); }
        }
        internal void TraceHotelPoseCatalog(string stage, object source, bool allowed)
        {
            if (_diagnostics?.Invoke() != true || _hSession?.Busy != true || _roomRestore == null) return;
            _hSession.TraceRoomPoseCatalog(stage, source, allowed);
        }
        private void LoadRoomStateForRestore(string room)
        {
            _roomRestore = new HotelRoomRestoreBatch(_diagnostics, _log, _roomStateStore.FilePath);
            _roomState = new HotelRoomState { room = room };
            _roomRestore.Item("客室ファイル読込", "$", "HotelRoomState", () => HotelRoomStateReader.Read(_roomStateStore.FilePath, room),
                result =>
                {
                    _roomState = result.State;
                    foreach (var error in result.Errors) _roomRestore.Failure("JSON項目解析", error.Key, error.Value);
                }, summary: result => "room=" + result.State.room + " parsedErrors=" + result.Errors.Count,
                actual: () => "room=" + _roomState.room + " selection=" + _roomState.selection?.name);
            _roomRestore.Observe("source-identity", () =>
            {
                var file = new FileInfo(_roomStateStore.FilePath);
                return "length=" + (file.Exists ? file.Length.ToString() : "missing") + " modifiedUtc=" + (file.Exists ? file.LastWriteTimeUtc.ToString("O") : "missing");
            });
        }
        private ChaFileControl PrepareRoomCard(bool female, ChaFileControl fallback)
        {
            ChaFileControl restored = fallback;
            _roomRestore.Item(female ? "女性カード入力" : "男性カード入力", female ? "appearance.female" : "appearance.male", "OpenHData.card", () =>
            {
                var appearance = _roomState.appearance;
                if (appearance == null || !string.Equals(appearance.source, _roomSource, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("保存カードの登録元が現在の登録カードと一致しない、または外見データがありません。");
                string encoded = female ? appearance.female : appearance.male;
                if (string.IsNullOrWhiteSpace(encoded)) throw new InvalidDataException("保存カードが欠損しています。");
                if (female && !appearance.CanRestoreCard(_roomState.room))
                    throw new InvalidDataException("旧201保存形式のためカード全体を復元できません。登録カードを維持します。");
                return HotelRoomAppearanceRuntime.Decode(encoded);
            }, card => restored = card, card => restored != null,
                card => HotelRoomAppearanceRuntime.StatusSummary(card.status), () => restored == null ? "registered-default" : HotelRoomAppearanceRuntime.StatusSummary(restored.status));
            return restored;
        }
        private void RestoreRoomReadyState()
        {
            _restoringRoomSelection = true;
            try
            {
                _roomRestore.Item("体位", "selection", "HSceneProc.flags.nowAnimationInfo", () => _roomState.selection ?? throw new InvalidDataException("保存体位が欠損しています。"), value =>
                {
                    if (!_hSession.RestoreRoomSelection(value, out string reason)) throw new InvalidOperationException(reason);
                }, value => value.Matches(_hSession.ReadRoomSelection()),
                value => value.mode + "/" + value.id + "/" + value.name,
                () => _hSession.ReadRoomSelection()?.name ?? "none", () =>
                {
                    var before = _hSession.ReadRoomSelection();
                    return () => { if (before != null && !_hSession.RestoreRoomSelection(before, out string reason)) throw new InvalidOperationException(reason); };
                });
            }
            finally { _restoringRoomSelection = false; }
            if (!_mapPlacementHandled) _roomRestore.Item("位置・向き", "placement", "RoomActors.root", () =>
            {
                var value = _roomState.placement ?? throw new InvalidDataException("保存配置が欠損しています。"); value.Validate(); return value;
            }, value => _hSession.ApplyRoomPlacement(_sessionRoomRoot, value), value =>
            {
                var actual = _hSession.ReadRoomPlacement(_sessionRoomRoot);
                return Mathf.Approximately(actual.x, value.x) && Mathf.Approximately(actual.y, value.y) && Mathf.Approximately(actual.z, value.z)
                    && Mathf.Approximately(Mathf.DeltaAngle(actual.yaw, value.yaw), default(float));
            }, PlacementSummary, () => PlacementSummary(_hSession.ReadRoomPlacement(_sessionRoomRoot)), () =>
            {
                var before = _hSession.ReadRoomPlacement(_sessionRoomRoot);
                return () => { _hSession.ApplyRoomPlacement(_sessionRoomRoot, before); _livePlacement = before.Copy(); };
            });
            var appearance = _roomState.appearance;
            if (appearance != null && !string.Equals(appearance.source, _roomSource, StringComparison.OrdinalIgnoreCase)) appearance = null;
            HotelRoomAppearanceRuntime.RestoreIndependent(_hSession, appearance, _roomControls.preferences, _roomRestore);
            HotelRoomActorEffects.Restore(_hSession, appearance, _roomRestore);
        }
        private void FinishRoomRestore()
        {
            if (!_mapPlacementHandled && _hSession?.RoomReady == true && _roomState?.placement != null)
                _roomRestore.Item("配置の最終照合", "placement", "RoomActors.root after category-location", () => _roomState.placement, _ => { }, value =>
                {
                    var actual = _hSession.ReadRoomPlacement(_sessionRoomRoot);
                    bool same = Mathf.Approximately(actual.x, value.x) && Mathf.Approximately(actual.y, value.y) && Mathf.Approximately(actual.z, value.z)
                        && Mathf.Approximately(Mathf.DeltaAngle(actual.yaw, value.yaw), default(float));
                    if (!same) throw new InvalidOperationException("客室配置の適用後、体位場所の自動適用を経て保存配置と相違しています。");
                    return true;
                }, PlacementSummary, () => PlacementSummary(_hSession.ReadRoomPlacement(_sessionRoomRoot)));
            _roomRestore.Finish();
            if (_roomRestore.HasNotice) Plugin.ReportHotelSaveResult(false, _roomRestore.Status);
            _placementPanel?.Status(_roomRestore.Status);
        }
    }
}
