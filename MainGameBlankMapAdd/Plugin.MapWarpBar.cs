using UnityEngine;

namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private void DrawMapWarpBar(Rect rect)
        {
            bool enabled = GUI.enabled;
            try
            {
                bool available = TryMapWarpContext(out string key, out Transform root, out _) && _mapWarpDocument != null;
                if (available) PrepareMapWarpNavigation(key, root);
                bool canStep = available && _hPlaceDraft == null && !_hPlaceBusy && MapLocationNavigation.Ids(_mapWarpDocument, _hLocations, key).Count > 0;
                float x = rect.x;
                GUI.Label(new Rect(x, rect.y + 2f, 58f, 20f), "マップ位置"); x += 60f;
                GUI.enabled = enabled && canStep;
                if (GUI.Button(new Rect(x, rect.y, 24f, 21f), new GUIContent("◁", "このマップの前のH場所・登録地点へ移動（カメラと体位はそのまま）"))) StepMapWarpPoint(-1);
                x += 26f;
                GUI.enabled = enabled && available && _hPlaceDraft == null && !_hPlaceBusy && MapWarpOrigin.Find(_mapWarpDocument, key) != null;
                if (GUI.Button(new Rect(x, rect.y, 24f, 21f), new GUIContent("□", "保存済みのデフォルトプリセット「元の場所」へ戻す"))) ReturnMapWarpOrigin();
                x += 26f;
                GUI.enabled = enabled && canStep;
                if (GUI.Button(new Rect(x, rect.y, 24f, 21f), new GUIContent("▷", "このマップの次のH場所・登録地点へ移動（カメラと体位はそのまま）"))) StepMapWarpPoint(1);
                x += 26f;
                GUI.enabled = enabled;
                string label = available ? (_mapWarpActiveId == null ? "現在の配置" : "") : "";
                if (available && _mapWarpActiveId != null)
                    foreach (MapWarpPoint point in _mapWarpRows) if (point.Id == _mapWarpActiveId) { label = point.Name; break; }
                if (available && _mapWarpRows.Count == 0) label = "未登録";
                if (available && _hPlaceActive != null && _hPlaceActive.MapKey == key) label = _hPlaceActive.Name;
                GUI.Label(new Rect(x, rect.y, Mathf.Max(0f, rect.xMax - x), 21f), new GUIContent(label, _mapWarpError ?? label));
            }
            finally { GUI.enabled = enabled; }
        }
    }
}
