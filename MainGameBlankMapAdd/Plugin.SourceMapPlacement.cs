using System.Globalization;
using UnityEngine;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private Transform _sourcePlacementRoot;
        private Vector3 _sourcePlacementPosition, _sourcePlacementAnchor;
        private Quaternion _sourcePlacementRotation;

        // Bind only once per source root. No scene searches or frame watcher are needed.
        private bool TrySourceMapWarpContext(out string key, out Transform root)
        {
            key = null;
            root = LastReservedMap?.mapRoot != null ? LastReservedMap.mapRoot.transform : null;
            if (root == null) return false;
            if (_sourcePlacementRoot != root)
            {
                RestoreSourceMapPlacement();
                _sourcePlacementRoot = root;
                _sourcePlacementPosition = root.position;
                _sourcePlacementRotation = root.rotation;
                _sourcePlacementAnchor = _femaleChara.transform.position;
            }
            string number = _hotelPlaybackSession ? "hotel-room-" + _hotelSourceRoomId :
                LastReservedMap.no.ToString(CultureInfo.InvariantCulture);
            // Native maps use the same identity when selected as an overlay or as the source.
            if (_aiSourceMap != null) key = "special:ai_shoujo_main";
            else if (!_hotelPlaybackSession && LastReservedMap.infoDic != null && LastReservedMap.infoDic.TryGetValue(LastReservedMap.no, out var info) &&
                info != null && !string.IsNullOrEmpty(info.AssetBundleName) && !string.IsNullOrEmpty(info.AssetName))
                key = "bundle:" + info.AssetBundleName.Replace('\\', '/') + "\nscene:" + info.AssetName;
            else key = "source:" + number;
            if (_roomOverlayPositionRoot != root.gameObject)
            {
                _roomOverlayPositionRoot = root.gameObject;
                _roomOverlayPositionKey = "source-" + number;
                _roomOverlayBasePosition = _sourcePlacementPosition;
                _roomOverlayBaseRotation = _sourcePlacementRotation;
                _roomOverlayActorAnchor = _sourcePlacementAnchor;
                EnsureRoomOverlayPositionConfig(_roomOverlayPositionKey);
                ApplyRoomOverlayPosition();
                _mapTabInputKey = null;
            }
            return true;
        }

        private void RestoreSourceMapPlacement()
        {
            if (_sourcePlacementRoot != null)
                _sourcePlacementRoot.SetPositionAndRotation(_sourcePlacementPosition, _sourcePlacementRotation);
            _sourcePlacementRoot = null;
        }
    }
}
