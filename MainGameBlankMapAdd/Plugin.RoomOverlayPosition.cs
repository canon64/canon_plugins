using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private sealed class RoomOverlayPosition
        {
            internal ConfigEntry<float> X;
            internal ConfigEntry<float> Y;
            internal ConfigEntry<float> Z;
            internal ConfigEntry<float> RotationX;
            internal ConfigEntry<float> RotationY;
            internal ConfigEntry<float> RotationZ;
            internal ConfigEntry<bool> RotateAroundActor;
        }

        private readonly Dictionary<string, RoomOverlayPosition> _roomOverlayPositions =
            new Dictionary<string, RoomOverlayPosition>();


        private RoomOverlayPosition EnsureRoomOverlayPositionConfig(string key)
        {
            RoomOverlayPosition position;
            if (_roomOverlayPositions.TryGetValue(key, out position)) return position;
            string section = "11.RoomOverlayPosition." + key;
            position = new RoomOverlayPosition
            {
                X = Config.Bind(section, "PositionX", 0f, "自動配置位置からの左右オフセット"),
                Y = Config.Bind(section, "PositionY", 0f, "自動配置位置からの上下オフセット"),
                Z = Config.Bind(section, "PositionZ", 0f, "自動配置位置からの前後オフセット"),
                RotationX = Config.Bind(section, "RotationX", 0f, "マップのX軸回転角度"),
                RotationY = Config.Bind(section, "RotationY", 0f, "マップのY軸回転角度"),
                RotationZ = Config.Bind(section, "RotationZ", 0f, "マップのZ軸回転角度"),
                RotateAroundActor = Config.Bind(section, "RotateAroundActor", false,
                    "true: 読み込み時のキャラ位置を中心にマップを回転")
            };
            _roomOverlayPositions.Add(key, position);
            return position;
        }

        private void ApplyRoomOverlayPosition()
        {
            if (_roomOverlayPositionRoot == null || string.IsNullOrEmpty(_roomOverlayPositionKey)) return;
            RoomOverlayPosition value = EnsureRoomOverlayPositionConfig(_roomOverlayPositionKey);
            Quaternion rotation = _roomOverlayBaseRotation *
                Quaternion.Euler(value.RotationX.Value, value.RotationY.Value, value.RotationZ.Value);
            Vector3 position = _roomOverlayBasePosition + new Vector3(value.X.Value, value.Y.Value, value.Z.Value);
            if (value.RotateAroundActor.Value)
            {
                Quaternion delta = rotation * Quaternion.Inverse(_roomOverlayBaseRotation);
                position = _roomOverlayActorAnchor + delta * (position - _roomOverlayActorAnchor);
            }
            Transform root = _roomOverlayPositionRoot.transform;
            if (root.position != position || root.rotation != rotation)
            {
                root.SetPositionAndRotation(position, rotation);
                if (_aiIslandActive) _aiVisibility?.RequestRefresh();
            }
        }

    }
}
