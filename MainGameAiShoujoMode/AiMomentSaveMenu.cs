using System;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    // Drawn by the existing I-key menu; that window owns input and cursor capture.
    internal sealed class AiMomentSaveMenu
    {
        private readonly Func<string> _save, _load;
        private readonly Action _help;
        private string _message = "セーブは1枠です。ロードすると最後にセーブした状態へ戻ります。";
        internal AiMomentSaveMenu(Func<string> save, Func<string> load, Action help)
        { _save = save; _load = load; _help = help; }
        internal void Draw()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            try
            {
                GUILayout.Label("セーブ／ロード");
                GUILayout.BeginHorizontal();
                try
                {
                    if (GUILayout.Button("セーブ", GUILayout.ExpandWidth(false))) Run(_save);
                    if (GUILayout.Button("ロード", GUILayout.ExpandWidth(false))) Run(_load);
                    if (GUILayout.Button("操作説明", GUILayout.ExpandWidth(false))) _help();
                }
                finally { GUILayout.EndHorizontal(); }
                GUILayout.Label(_message);
            }
            finally { GUILayout.EndVertical(); }
        }
        private void Run(Func<string> command)
        {
            try { _message = command(); }
            catch (Exception ex) { _message = ex.GetBaseException().Message; }
        }
    }
}
