using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MainGirlHipHijack
{
    /// <summary>
    /// MainGameObjectComposer の公開 API へリフレクション経由でアクセスする。
    /// ObjectComposer 未導入時は IsAvailable=false で全 API が無効化される。
    /// </summary>
    internal static class ObjectComposerBridge
    {
        private static Type _composerPluginType;
        private static Type _objectInfoType;
        private static FieldInfo _fiInfoId;
        private static FieldInfo _fiInfoName;
        private static MethodInfo _miTryGetList;
        private static MethodInfo _miTryGetTransform;
        private static bool _lookupDone;
        private static bool _lookupOk;

        private static readonly object[] _listArgs = new object[1];
        private static readonly object[] _tfArgs = new object[2];

        internal static bool IsAvailable => Resolve();

        private static bool Resolve()
        {
            if (_lookupDone) return _lookupOk;
            _lookupDone = true;

            _composerPluginType =
                AccessTools.TypeByName("MainGameObjectComposer.Plugin") ??
                Type.GetType("MainGameObjectComposer.Plugin, MainGameObjectComposer");

            if (_composerPluginType == null)
            {
                return false;
            }

            _miTryGetList = _composerPluginType.GetMethod(
                "TryGetManagedObjectList",
                BindingFlags.Public | BindingFlags.Static);
            _miTryGetTransform = _composerPluginType.GetMethod(
                "TryGetManagedObjectTransform",
                BindingFlags.Public | BindingFlags.Static);

            _objectInfoType =
                AccessTools.TypeByName("MainGameObjectComposer.ComposerObjectInfo") ??
                Type.GetType("MainGameObjectComposer.ComposerObjectInfo, MainGameObjectComposer");

            if (_objectInfoType != null)
            {
                _fiInfoId = _objectInfoType.GetField("id", BindingFlags.Public | BindingFlags.Instance);
                _fiInfoName = _objectInfoType.GetField("name", BindingFlags.Public | BindingFlags.Instance);
            }

            _lookupOk = _miTryGetList != null && _miTryGetTransform != null && _fiInfoId != null && _fiInfoName != null;
            return _lookupOk;
        }

        internal sealed class Entry
        {
            public string Id;
            public string Name;
        }

        internal static bool TryGetObjectList(out List<Entry> entries)
        {
            entries = null;
            if (!Resolve()) return false;

            _listArgs[0] = null;
            bool ok;
            try
            {
                ok = (bool)_miTryGetList.Invoke(null, _listArgs);
            }
            catch (Exception)
            {
                return false;
            }
            if (!ok) return false;

            IList raw = _listArgs[0] as IList;
            if (raw == null) return false;

            entries = new List<Entry>(raw.Count);
            for (int i = 0; i < raw.Count; i++)
            {
                object o = raw[i];
                if (o == null) continue;
                string id = _fiInfoId.GetValue(o) as string;
                string name = _fiInfoName.GetValue(o) as string;
                entries.Add(new Entry { Id = id, Name = name });
            }
            return true;
        }

        internal static bool TryGetTransform(string id, out Transform transform)
        {
            transform = null;
            if (!Resolve()) return false;
            if (string.IsNullOrEmpty(id)) return false;

            _tfArgs[0] = id;
            _tfArgs[1] = null;
            bool ok;
            try
            {
                ok = (bool)_miTryGetTransform.Invoke(null, _tfArgs);
            }
            catch (Exception)
            {
                return false;
            }
            if (!ok) return false;
            transform = _tfArgs[1] as Transform;
            return transform != null;
        }

        internal static string GetObjectName(string id)
        {
            if (!TryGetObjectList(out List<Entry> entries) || entries == null) return null;
            for (int i = 0; i < entries.Count; i++)
            {
                if (string.Equals(entries[i].Id, id, StringComparison.Ordinal))
                {
                    return entries[i].Name;
                }
            }
            return null;
        }
    }
}
