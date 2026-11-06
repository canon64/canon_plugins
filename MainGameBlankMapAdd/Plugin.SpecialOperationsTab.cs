using UnityEngine;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        // 特殊操作の共通入口。今後の操作は個別の描画関数としてここへ追加する。
        private GUIStyle _specialOperationsHelpStyle;

        private void DrawEmbeddedSpecialOperationsTab(float availableWidth)
        {
            if (_specialOperationsHelpStyle == null)
                _specialOperationsHelpStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
            GUILayout.Label("特殊操作", GUI.skin.box);
            DrawKsPlugTimeOperation(Mathf.Max(280f, availableWidth));
        }
    }
}
