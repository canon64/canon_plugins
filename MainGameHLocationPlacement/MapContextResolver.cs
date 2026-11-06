using System;
using UnityEngine;

namespace MainGameHLocationPlacement
{
    // Read the current owned context only. Never reserve or reposition a map here.
    internal sealed class MapContextInput
    {
        internal bool MapActive, Hotel, Ai;
        internal string Scene, Bundle, Display, RoomId, SourceKey, SourceName;
        internal Transform OverlayRoot, HotelPivot, SourceRoot, BoundRoot;
        internal string BoundKey;
    }
    internal sealed class ResolvedMapContext
    {
        internal Transform Root;
        internal string Key, Name;
    }
    internal static class MapContextResolver
    {
        internal static ResolvedMapContext Resolve(MapContextInput input)
        {
            Transform root;
            string key, name;
            if (input.MapActive && !string.IsNullOrEmpty(input.Scene))
            {
                root = input.Scene == "thehotelroom_lit" ? input.HotelPivot : input.OverlayRoot;
                key = input.Scene == "thehotelroom_lit" ? "special:hotel" :
                    input.Scene == "map_00_data" ? "special:ai_shoujo_main" :
                    !string.IsNullOrEmpty(input.Bundle) ? "bundle:" + input.Bundle.Replace('\\', '/') + "\nscene:" + input.Scene : "scene:" + input.Scene;
                name = string.IsNullOrEmpty(input.Display) ? input.Scene : input.Display;
            }
            else
            {
                root = input.SourceRoot;
                if (input.Hotel && string.IsNullOrEmpty(input.RoomId)) throw new InvalidOperationException("ホテルの客室番号が未準備です。");
                key = input.Hotel ? "source:hotel-room-" + input.RoomId :
                    input.Ai ? "special:ai_shoujo_main" : input.SourceKey;
                name = input.Hotel ? "ホテル " + input.RoomId : input.SourceName;
            }
            if (root == null || string.IsNullOrEmpty(key)) throw new InvalidOperationException("現在のマップの配置基準が未準備です。");
            // A previous map's selected H place must not override the owned source.
            if (input.MapActive && input.BoundRoot == root && !string.IsNullOrEmpty(input.BoundKey)) key = input.BoundKey;
            return new ResolvedMapContext { Root = root, Key = key, Name = name ?? root.gameObject.name };
        }
    }
}
