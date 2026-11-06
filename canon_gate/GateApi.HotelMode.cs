using System;
using UnityEngine;

namespace CanonGate
{
    public static partial class GateApi
    {
        public static HotelModeScope HotelMode { get; private set; }
        public static HotelModeScope EnterHotelMode(MonoBehaviour owner)
            => EnterHotelMode(owner, "201");
        public static HotelModeScope EnterHotelMode(MonoBehaviour owner, string roomId)
        {
            if (owner == null || HotelMode != null || Current != null) throw new InvalidOperationException("ホテルのH接続は既に使用中です。");
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("ホテルの客室番号がありません。", nameof(roomId));
            return HotelMode = new HotelModeScope(owner, roomId);
        }
        public sealed class HotelModeScope : IDisposable
        {
            public MonoBehaviour Owner { get; }
            public string RoomId { get; }
            private MonoBehaviour _scene;
            private bool _disposed;
            internal HotelModeScope(MonoBehaviour owner, string roomId) { Owner = owner; RoomId = roomId; }
            public void Bind(MonoBehaviour owner, MonoBehaviour scene)
            {
                if (_disposed || !ReferenceEquals(owner, Owner) || scene == null || _scene != null) throw new InvalidOperationException("ホテルのシーン参照が不正です。");
                _scene = scene;
            }
            internal bool Owns(MonoBehaviour scene) => !_disposed && ReferenceEquals(_scene, scene) && scene != null;
            public void Dispose()
            {
                if (_disposed) return; _disposed = true;
                if (Current?.IsHotelMode == true && ReferenceEquals(Current.Scene, _scene)) EndSession(SessionKind.H);
                if (ReferenceEquals(HotelMode, this)) HotelMode = null;
                _scene = null;
            }
        }
    }
}
