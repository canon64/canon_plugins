using System;

namespace MainGameBlankMapAdd.Map
{
    internal sealed class MapSessionController
    {
        private readonly SourceMapBlankifier _blankifier;
        private readonly Action<BaseMap> _ensureRoom;
        private readonly Action _destroyRoom;
        private readonly SourceMapVisibilityPreparation _sourceVisibility = new SourceMapVisibilityPreparation();

        internal MapSessionController(SourceMapBlankifier blankifier, Action<BaseMap> ensureRoom, Action destroyRoom)
        {
            _blankifier = blankifier ?? throw new ArgumentNullException(nameof(blankifier));
            _ensureRoom = ensureRoom ?? throw new ArgumentNullException(nameof(ensureRoom));
            _destroyRoom = destroyRoom ?? throw new ArgumentNullException(nameof(destroyRoom));
        }

        internal BaseMap LastReservedMap { get; private set; }
        internal void Reserve(BaseMap map) { LastReservedMap = map; }
        internal void PrepareSourceMapVisibility(PluginSettings settings)
        {
            if (LastReservedMap?.mapRoot == null || settings == null || LastReservedMap.no != settings.AddedMapNo)
            {
                CancelSourceMapPreparation();
                return;
            }
            _sourceVisibility.Prepare(LastReservedMap.mapRoot, settings);
        }
        internal void CancelSourceMapPreparation() { _sourceVisibility.Reset(); }
        internal void Stop() { _blankifier.Reset(); CancelSourceMapPreparation(); _destroyRoom(); }

        internal void Apply(BaseMap map, PluginSettings settings)
        {
            // Transfer original visibility to the runtime blankifier synchronously,
            // before any rendering can occur between restoration and blankification.
            CancelSourceMapPreparation();
            LastReservedMap = map;
            if (map?.mapRoot == null || settings == null || map.no != settings.AddedMapNo)
            {
                _blankifier.Reset();
                _destroyRoom();
                return;
            }

            _blankifier.Apply(map, settings);
            if (settings.EnableVideoRoom) _ensureRoom(map);
            else _destroyRoom();
        }

        internal void ForceReapply(PluginSettings settings)
        {
            _blankifier.Reset();
            Apply(LastReservedMap, settings);
        }
    }
}
