using System;
using System.Collections.Generic;
using System.Reflection;
using ActionGame;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    // HSceneProc reuses ActionScene.Map when native VR walking is present.
    // Borrow that exact map for this H boundary, then restore the walking owner.
    internal sealed class HSessionBorrowedMapScope : IDisposable
    {
        private readonly ActionMap _map;
        private readonly List<KeyValuePair<MemberInfo, object>> _before = new List<KeyValuePair<MemberInfo, object>>();
        private bool _disposed;
        private static MemberInfo Member(string name)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            for (var t = typeof(ActionMap); t != null; t = t.BaseType)
            {
                var value = (MemberInfo)t.GetProperty(name, flags) ?? t.GetField(name, flags);
                if (value != null) return value;
            }
            throw new MissingMemberException(typeof(ActionMap).FullName, name);
        }
        private object Read(MemberInfo member) => member is PropertyInfo p ? p.GetValue(_map, null) : ((FieldInfo)member).GetValue(_map);
        private void Write(MemberInfo member, object value)
        {
            if (member is PropertyInfo p) p.SetValue(_map, value, null);
            else ((FieldInfo)member).SetValue(_map, value);
        }
        internal HSessionBorrowedMapScope(ActionMap map, GameObject root, int number)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            if (root == null) throw new ArgumentNullException(nameof(root));
            foreach (var name in new[] { "mapRoot", "no", "isMapLoading", "_isLock", "mapObjects", "mapObjectGroup" })
            {
                var member = Member(name);
                _before.Add(new KeyValuePair<MemberInfo, object>(member, Read(member)));
            }
            try
            {
                Write(Member("_isLock"), true);
                Write(Member("mapRoot"), root);
                Write(Member("no"), number);
                Write(Member("isMapLoading"), false);
                Write(Member("mapObjects"), Array.Empty<ActionGame.MapObject.Kind>());
                Write(Member("mapObjectGroup"), root.transform);
            }
            catch { Dispose(); throw; }
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_map == null) return;
            Exception failure = null;
            for (int i = _before.Count; i > default(int);)
            {
                var value = _before[--i];
                try { Write(value.Key, value.Value); }
                catch (Exception ex) { if (failure == null) failure = ex; }
            }
            if (failure != null) throw new InvalidOperationException("歩行用マップの復元に失敗しました。", failure);
        }
    }
}
