using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CanonPlugins.Shared;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private BaseMap _hotelSourceMap;
        private BaseMap _aiSourceMap;
        private string _hotelSourceRoomId;
        private IReadOnlyDictionary<int, MapInfo.Param> _roomChoiceCatalog;
        private BaseMap LastReservedMap => _hotelPlaybackSession ? _hotelSourceMap : _aiSourceMap != null ? _aiSourceMap : _mapSession?.LastReservedMap;

        private void BindOwnedMapContext()
        {
            // Hotel H deliberately skips BaseMap.Reserve. Bind its actual room once,
            // without accepting a stale reservation from an earlier native scene.
            _hotelSourceMap = _hotelPlaybackSession && PluginSessionGate.HScene != null
                ? PluginSessionGate.HScene.GetComponent<BaseMap>() ??
                    typeof(HSceneProc).GetField("map", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(PluginSessionGate.HScene) as BaseMap : null;
            _hotelSourceRoomId = _hotelPlaybackSession ? CanonGate.GateApi.Current?.HotelRoomId : null;
            _aiSourceMap = CanonGate.GateApi.Current?.IsAiShoujoMode == true && PluginSessionGate.HScene != null
                ? PluginSessionGate.HScene.GetComponent<BaseMap>() : null;
            _roomChoiceCatalog = null;
            _roomOverlayChoices.Clear();
        }

        private void ClearOwnedMapContext()
        {
            _hotelSourceMap = null;
            _aiSourceMap = null;
            _hotelSourceRoomId = null;
            _roomChoiceCatalog = null;
            _roomOverlayChoices.Clear();
        }

        private void RefreshRoomOverlayChoices()
        {
            var catalog = CanUseRoomOverlay() ? LastReservedMap?.infoDic : null;
            if (ReferenceEquals(catalog, _roomChoiceCatalog)) return;
            _roomChoiceCatalog = catalog;
            _roomOverlayChoices.Clear();
            if (catalog == null) return;
            _roomOverlayChoices.AddRange(catalog.Values.Where(p => p != null &&
                p.No != _settings.AddedMapNo && !string.IsNullOrEmpty(p.AssetBundleName) &&
                !string.IsNullOrEmpty(p.AssetName)).OrderBy(p => p.No));
        }
    }
}
