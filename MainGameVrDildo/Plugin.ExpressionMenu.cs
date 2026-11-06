using System;
using System.Collections.Generic;
using UnityEngine;

namespace MainGameVrDildo
{
    public sealed partial class Plugin
    {
        private List<KeyValuePair<int, string>> _eyeChoices = new List<KeyValuePair<int, string>>(ExpressionNames.Eyes);
        private List<KeyValuePair<int, string>> _eyelidChoices = new List<KeyValuePair<int, string>>(ExpressionNames.Eyelids);
        private List<KeyValuePair<int, string>> _eyebrowChoices = new List<KeyValuePair<int, string>>(ExpressionNames.Eyebrows);
        private List<KeyValuePair<int, string>> _mouthChoices = new List<KeyValuePair<int, string>>(ExpressionNames.Mouth);
        private string _expressionMenuKey, _expressionMenuSearch = "";
        private Vector2 _expressionMenuScroll;
        private int[] _expressionMenuSlots;
        private int _expressionMenuSlot;
        private bool _expressionMenuMouth;
        private string _expressionMenuTitle;
        private List<KeyValuePair<int, string>> _expressionMenuChoices;
        private Rect _expressionMenuRect;
        private GUIStyle _expressionRowStyle;

        // Called on target changes, settings-open and the explicit reload button only.
        private void RebuildExpressionMenus()
        {
            if (!CanonPlugins.Shared.PluginSessionGate.Allowed) return;
            var eyes = new Dictionary<int, string>();
            foreach (var entry in ExpressionNames.Eyes) eyes[entry.Key] = entry.Value;
            var registered = _female?.lstCtrl?.GetCategoryInfo(ChaListDefine.CategoryNo.cha_eyeset);
            if (registered != null) foreach (var entry in registered)
            {
                string name = entry.Value?.GetInfo(ChaListDefine.KeyType.Name);
                if (!string.IsNullOrWhiteSpace(name)) eyes[entry.Key] = name;
            }
            _eyeChoices = SortedExpressionChoices(eyes);
            _eyelidChoices = ControllerExpressionChoices(ExpressionNames.Eyelids, _female?.eyesCtrl);
            _eyebrowChoices = ControllerExpressionChoices(ExpressionNames.Eyebrows, _female?.eyebrowCtrl);
            _mouthChoices = ControllerExpressionChoices(ExpressionNames.Mouth, _female?.mouthCtrl);
            CloseExpressionMenu();
        }

        private static List<KeyValuePair<int, string>> SortedExpressionChoices(Dictionary<int, string> names)
        {
            var entries = new List<KeyValuePair<int, string>>(names);
            entries.Sort((a, b) => a.Key.CompareTo(b.Key)); return entries;
        }
        private static List<KeyValuePair<int, string>> ControllerExpressionChoices(KeyValuePair<int, string>[] known, FBSBase controller)
        {
            var names = new Dictionary<int, string>();
            int count = controller?.GetMaxPtn() ?? 0;
            foreach (var entry in known) if (count <= 0 || entry.Key < count) names[entry.Key] = entry.Value;
            for (int id = 0; id < count; id++)
            {
                if (names.ContainsKey(id)) continue;
                string label = null;
                if (controller.FBSTarget != null) foreach (var target in controller.FBSTarget)
                {
                    if (target?.PtnSet == null || id >= target.PtnSet.Length) continue;
                    var pattern = target.PtnSet[id]; var mesh = target.GetSkinnedMeshRenderer()?.sharedMesh;
                    if (pattern == null || mesh == null) continue;
                    int shape = pattern.Close >= 0 && pattern.Close < mesh.blendShapeCount ? pattern.Close : pattern.Open;
                    if (shape >= 0 && shape < mesh.blendShapeCount) { label = mesh.GetBlendShapeName(shape); break; }
                }
                names[id] = string.IsNullOrWhiteSpace(label) ? "名称なし" : label;
            }
            return SortedExpressionChoices(names);
        }

        private bool DrawExpressionSlots(string label, string key, int[] slots, ref bool random, List<KeyValuePair<int, string>> choices, float[] openings = null)
        {
            bool changed = Toggle(label + "を5枠からランダム選択", ref random);
            if (slots == null || slots.Length != ExpressionSlotChoices.Count) return changed;
            for (int i = 0; i < ExpressionSlotChoices.Count; i++)
            {
                string slotKey = key + "_slot_" + i;
                string name = slots[i] < 0 ? "未使用" : "未登録の番号";
                foreach (var entry in choices) if (entry.Key == slots[i]) { name = entry.Value; break; }
                GUILayout.BeginHorizontal(); GUILayout.Label(label + " " + (char)('A' + i), GUILayout.Width(105));
                if (GUILayout.Button(slots[i] + "：" + name + " ▾", GUILayout.Width(330)))
                {
                    if (_expressionMenuKey == slotKey) { CloseExpressionMenu(); }
                    else
                    {
                        _expressionMenuKey = slotKey;
                        _expressionMenuSlots = slots; _expressionMenuSlot = i;
                        _expressionMenuChoices = choices; _expressionMenuMouth = key == "mouth";
                        _expressionMenuTitle = label + " " + (char)('A' + i);
                        float width = Mathf.Min(460f, Screen.width - 16f);
                        float height = Mathf.Min(390f, Screen.height - 16f);
                        _expressionMenuRect = new Rect((Screen.width - width) * .5f,
                            (Screen.height - height) * .5f, width, height);
                        SyncExpressionMenuInput();
                    }
                    _expressionMenuSearch = ""; _expressionMenuScroll = Vector2.zero;
                }
                GUILayout.EndHorizontal();
                if (openings != null && i < openings.Length)
                {
                    float previous = openings[i];
                    Float("  " + (char)('A' + i) + " の開閉度", slotKey + "_opening", ref openings[i], 0f, 1f);
                    if (!Mathf.Approximately(previous, openings[i])) PreviewEyelidSlot(i);
                }
            }
            return changed;
        }

        private void CloseExpressionMenu()
        {
            _expressionMenuKey = null; _expressionMenuSlots = null; _expressionMenuChoices = null;
            ClearExpressionMenuInput();
        }

        // A separate, explicitly sized window: the host tab's scroll layout cannot shrink its list.
        private void DrawExpressionMenu()
        {
            if (_expressionMenuKey == null) return;
            if (!Application.isFocused) { CloseExpressionMenu(); return; }
            var ev = Event.current;
            if ((ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape) ||
                (ev.type == EventType.MouseDown && !_expressionMenuRect.Contains(ev.mousePosition)))
            { CloseExpressionMenu(); ev.Use(); return; }
            int depth = GUI.depth, previousHot = GUIUtility.hotControl;
            Color color = GUI.color, background = GUI.backgroundColor, content = GUI.contentColor;
            try
            {
                GUI.depth = -10000; GUI.color = GUI.backgroundColor = GUI.contentColor = Color.white;
                _expressionMenuRect = GUI.ModalWindow(0x56524445, _expressionMenuRect,
                    DrawExpressionMenuWindow, "", GUIStyle.none);
            }
            finally
            {
                GUI.depth = depth; GUI.color = color; GUI.backgroundColor = background; GUI.contentColor = content;
                if (_expressionMenuKey != null)
                {
                    int hot = GUIUtility.hotControl;
                    if (hot != 0 && hot != previousHot) _expressionMenuHotControl = hot;
                    if (hot == 0) _expressionMenuHotControl = 0;
                    SyncExpressionMenuInput();
                }
            }
        }

        private void DrawExpressionMenuWindow(int id)
        {
            float width = _expressionMenuRect.width, height = _expressionMenuRect.height;
            GUI.color = new Color(.08f, .09f, .14f, 1f);
            GUI.DrawTexture(new Rect(0, 0, width, height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(12, 8, width - 100, 26), _expressionMenuTitle + " の候補を選択");
            bool close = GUI.Button(new Rect(width - 70, 8, 58, 26), "閉じる");
            GUI.Label(new Rect(12, 42, 46, 26), "検索");
            _expressionMenuSearch = GUI.TextField(new Rect(60, 42, width - 72, 26), _expressionMenuSearch);
            if (_expressionRowStyle == null)
                _expressionRowStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, wordWrap = false };
            var matches = new List<KeyValuePair<int, string>> { new KeyValuePair<int, string>(-1, "未使用") };
            foreach (var entry in _expressionMenuChoices)
                if (_expressionMenuSearch.Length == 0 || (entry.Key + "：" + entry.Value).IndexOf(
                    _expressionMenuSearch, StringComparison.OrdinalIgnoreCase) >= 0) matches.Add(entry);
            var viewport = new Rect(12, 78, width - 24, height - 112);
            _expressionMenuScroll = GUI.BeginScrollView(viewport, _expressionMenuScroll,
                new Rect(0, 0, viewport.width - 20, matches.Count * 28), false, true);
            int? selected = null;
            for (int i = 0; i < matches.Count; i++)
            {
                var entry = matches[i];
                bool current = _expressionMenuSlots[_expressionMenuSlot] == entry.Key;
                GUI.backgroundColor = current ? new Color(.3f, .65f, 1f) : Color.white;
                if (GUI.Button(new Rect(0, i * 28, viewport.width - 22, 26),
                    new GUIContent((current ? "● " : "    ") + entry.Key + "：" + entry.Value, entry.Value), _expressionRowStyle))
                    selected = entry.Key;
            }
            GUI.backgroundColor = Color.white;
            GUI.EndScrollView();
            GUI.Label(new Rect(12, height - 29, width - 24, 24), "選択すると即時反映 ／ Escで閉じる");
            if (selected.HasValue)
            {
                if (_expressionMenuSlots[_expressionMenuSlot] != selected.Value)
                {
                    _expressionMenuSlots[_expressionMenuSlot] = selected.Value; _settingsChanged = true;
                    RefreshSelectedEyeSettings(_expressionMenuMouth);
                    if (_expressionMenuKey.StartsWith("eyelid_slot_", StringComparison.Ordinal)) PreviewEyelidSlot(_expressionMenuSlot);
                }
                close = true;
            }
            if (close) CloseExpressionMenu();
        }
    }
}
