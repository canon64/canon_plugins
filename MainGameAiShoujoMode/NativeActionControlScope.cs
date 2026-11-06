using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace MainGameAiShoujoMode
{
    // Select the installed VRMOD's existing walking tool once; its own bindings handle all buttons.
    internal sealed class NativeActionControlScope : IDisposable
    {
        private readonly object _right, _selected;
        private readonly PropertyInfo _index, _enabled;
        private readonly int _previousIndex, _walkingIndex;
        private readonly bool _previousEnabled;
        private bool _disposed;
        internal NativeActionControlScope(Type vrType)
        {
            var mode = vrType?.GetProperty("Mode")?.GetValue(null, null);
            _right = mode?.GetType().GetProperty("Right")?.GetValue(mode, null)
                ?? throw new InvalidOperationException("本編VRMODの右コントローラーが準備されていません。");
            _index = _right.GetType().GetProperty("ToolIndex") ?? throw new MissingMemberException("VR controller ToolIndex");
            _enabled = _right.GetType().GetProperty("ToolEnabled") ?? throw new MissingMemberException("VR controller ToolEnabled");
            var tools = (AccessTools.Field(_right.GetType(), "Tools")?.GetValue(_right) as IEnumerable)?.Cast<object>().ToList()
                ?? throw new MissingMemberException("VR controller Tools");
            _selected = tools.Single(tool => tool.GetType().FullName == "KKS_VR.Controls.GameplayTool");
            _walkingIndex = tools.IndexOf(_selected);
            _previousIndex = (int)_index.GetValue(_right, null);
            _previousEnabled = (bool)_enabled.GetValue(_right, null);
            try { Select(_walkingIndex, true); }
            catch { Select(_previousIndex, _previousEnabled); throw; }
        }
        private void Select(int index, bool enabled)
        {
            _enabled.SetValue(_right, false, null);
            _index.SetValue(_right, index, null);
            _enabled.SetValue(_right, enabled, null);
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            // Preserve a tool explicitly selected by the user after entry.
            if ((int)_index.GetValue(_right, null) == _walkingIndex) Select(_previousIndex, _previousEnabled);
        }
    }
}
