using System;
using BepInEx.Bootstrap;
using UnityEngine;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private sealed class GadgetBarConnection
        {
            private readonly string _guid;
            private readonly string _defaultHint;
            private Action _primary, _delete;
            private Func<string> _hint;
            private string _error;
            internal GadgetBarConnection(string guid, string hint = "左クリック：初回だけ呼び出し→以後ギズモ表示／非表示 / 右クリック：削除")
            { _guid = guid; _defaultHint = hint; }
            internal string Hint => _error ?? (_hint != null ? _hint() : _defaultHint);
            internal void Invoke(bool delete)
            {
                try
                {
                    // Resolve only on an explicit button action, never from the draw loop.
                    if (_primary == null || _delete == null)
                    {
                        if (!Chainloader.PluginInfos.TryGetValue(_guid, out var info) || info.Instance == null)
                            throw new InvalidOperationException("対象プラグインが読み込まれていません。");
                        var type = info.Instance.GetType();
                        var primary = (Action)Delegate.CreateDelegate(typeof(Action), info.Instance, type.GetMethod("PlaybackBarPrimaryAction"));
                        var remove = (Action)Delegate.CreateDelegate(typeof(Action), info.Instance, type.GetMethod("PlaybackBarDelete"));
                        var hint = (Func<string>)Delegate.CreateDelegate(typeof(Func<string>), info.Instance, type.GetMethod("GetPlaybackBarActionHint"));
                        _primary = primary; _delete = remove; _hint = hint;
                    }
                    if (delete) _delete(); else _primary();
                    _error = null;
                }
                catch (Exception ex) { _error = "操作できません：" + ex.Message; }
            }
        }

        private void DrawGadgetBarButton(Rect rect, string label, GadgetBarConnection connection, GUIStyle style = null)
        {
            RegisterVrGadgetDelete(rect, label, () => connection.Invoke(true));
            // The PlaybackBar host registers this entire rectangle with UiInputCaptureApi.
            var current = Event.current;
            int control = GUIUtility.GetControlID(label.GetHashCode(), FocusType.Passive, rect);
            if (GUI.enabled && current != null && current.type == EventType.MouseDown && current.button == 1 && rect.Contains(current.mousePosition))
            {
                GUIUtility.hotControl = control;
                current.Use();
            }
            if (current != null && GUIUtility.hotControl == control && current.type == EventType.MouseUp && current.button == 1)
            {
                GUIUtility.hotControl = 0;
                if (GUI.enabled && rect.Contains(current.mousePosition)) connection.Invoke(true);
                current.Use();
            }
            if (GUI.Button(rect, new GUIContent(label, connection.Hint + "\nVR：ボタンを指してBクリックで削除"), style ?? GUI.skin.button)) connection.Invoke(false);
        }
    }
}
