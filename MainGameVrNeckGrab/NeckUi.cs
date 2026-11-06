using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MainGameVrNeckGrab
{
    internal sealed class NeckSlot : MonoBehaviour, IPointerClickHandler
    {
        Action _remove;
        Text _label;
        Image _background;
        bool _placed;
        internal static NeckSlot Create(HSprite sprite, int actorIndex, Action remove)
        {
            if (sprite.commonAibuIcons == null || sprite.commonAibuIcons.Length == 0) throw new InvalidOperationException("標準愛撫スロットがありません。");
            var reference = sprite.commonAibuIcons[sprite.commonAibuIcons.Length - 1];
            if (reference == null) throw new InvalidOperationException("標準愛撫スロットの配置元がありません。");
            var go = new GameObject("首スロット " + (actorIndex + 1), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = reference.gameObject.layer;
            var rect = (RectTransform)go.transform; rect.SetParent(reference.transform.parent, false);
            rect.anchorMin = reference.rectTransform.anchorMin; rect.anchorMax = reference.rectTransform.anchorMax; rect.pivot = reference.rectTransform.pivot;
            rect.sizeDelta = new Vector2(Mathf.Max(40, reference.rectTransform.rect.width), Mathf.Max(40, reference.rectTransform.rect.height));
            rect.anchoredPosition = reference.rectTransform.anchoredPosition + Vector2.right * (rect.sizeDelta.x + 6) * (actorIndex + 1);
            var slot = go.AddComponent<NeckSlot>(); slot._remove = remove; slot._background = go.GetComponent<Image>();
            slot._background.raycastTarget = true;
            var text = new GameObject("首", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var tr = (RectTransform)text.transform; tr.SetParent(rect, false); tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.sizeDelta = Vector2.zero;
            slot._label = text.GetComponent<Text>(); slot._label.font = sprite.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.font != null)?.font ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            slot._label.fontSize = 15; slot._label.alignment = TextAnchor.MiddleCenter; slot._label.raycastTarget = false;
            slot.SetPlaced(false); return slot;
        }
        internal void SetPlaced(bool placed)
        {
            _placed = placed;
            _background.color = placed ? new Color(.16f, .4f, .5f, .95f) : new Color(.05f, .12f, .18f, .7f);
            _label.text = placed ? "首\n解除" : "首";
            _label.color = placed ? Color.white : Color.gray;
        }
        public void OnPointerClick(PointerEventData e)
        {
            if (_placed && (e.button == PointerEventData.InputButton.Left || e.button == PointerEventData.InputButton.Right))
            { _remove(); e.Use(); }
        }
        internal void Dispose() { _remove = null; gameObject.SetActive(false); Destroy(gameObject); }
    }

    // A third, independently framed arc inside the original female arc. It never modifies
    // the two native Images, their sprites, fill modes, transforms or lock buttons.
    internal sealed class AwakeningArc : MaskableGraphic
    {
        float _amount;
        internal void SetAmount(float amount) { _amount = Mathf.Clamp01(amount / 100); SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = rectTransform.rect;
            float sx = rect.width / 192f, sy = rect.height / 232f;
            Vector2 center = new Vector2(rect.xMin + 22 * sx, rect.yMax - 34 * sy);
            Band(vh, center, sx, sy, 117, 131, 1, new Color32(0, 29, 50, 255));
            Band(vh, center, sx, sy, 119, 129, 1, new Color32(13, 65, 83, 255));
            Band(vh, center, sx, sy, 120, 128, _amount, new Color32(255, 216, 36, 255));
        }
        static void Band(VertexHelper vh, Vector2 center, float sx, float sy, float inner, float outer, float amount, Color32 color)
        {
            if (amount <= 0) return;
            int steps = Mathf.Max(1, Mathf.CeilToInt(48 * amount));
            for (int i = 0; i < steps; ++i)
            {
                float a = Mathf.Deg2Rad * Mathf.Lerp(-84, 0, amount * i / steps);
                float b = Mathf.Deg2Rad * Mathf.Lerp(-84, 0, amount * (i + 1) / steps);
                int start = vh.currentVertCount;
                foreach (var v in new[] { Point(center, a, inner, sx, sy), Point(center, a, outer, sx, sy), Point(center, b, outer, sx, sy), Point(center, b, inner, sx, sy) })
                    vh.AddVert(v, color, Vector2.zero);
                vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
            }
        }
        static Vector3 Point(Vector2 center, float angle, float radius, float sx, float sy)
            => new Vector3(center.x + Mathf.Cos(angle) * radius * sx, center.y + Mathf.Sin(angle) * radius * sy, 0);
    }
    public sealed partial class Plugin
    {
        AwakeningArc _awakening;
        Text _awakeningLabel;
        ActorState _displayed;
        void ShowAwakening(ActorState s)
        {
            if (!s.State.Doll) return;
            if (_awakening == null)
            {
                var source = _sprite.gauge[0].imgBar;
                if (source == null) throw new InvalidOperationException("女用の弧状感度ゲージがありません。");
                var go = new GameObject("覚醒ゲージ", typeof(RectTransform), typeof(CanvasRenderer));
                go.layer = source.gameObject.layer;
                var rect = (RectTransform)go.transform; rect.SetParent(source.transform.parent, false);
                var original = source.rectTransform;
                rect.anchorMin = original.anchorMin; rect.anchorMax = original.anchorMax; rect.pivot = original.pivot;
                rect.sizeDelta = original.sizeDelta; rect.anchoredPosition = original.anchoredPosition; rect.localRotation = original.localRotation; rect.localScale = original.localScale;
                // Draw after the native shared frame without changing its hierarchy or sprite.
                var gaugeRoot = source.transform.parent.parent;
                rect.SetParent(gaugeRoot, true); rect.SetAsLastSibling();
                _awakening = go.AddComponent<AwakeningArc>(); _awakening.raycastTarget = false;
                var label = new GameObject("覚醒ラベル", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                label.layer = go.layer;
                var lr = (RectTransform)label.transform; lr.SetParent(rect, false); lr.anchorMin = lr.anchorMax = new Vector2(0, 1); lr.pivot = new Vector2(0, 1); lr.sizeDelta = new Vector2(112, 42); lr.anchoredPosition = new Vector2(4, -70);
                _awakeningLabel = label.GetComponent<Text>(); _awakeningLabel.font = _sprite.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.font != null)?.font ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
                _awakeningLabel.fontSize = 13; _awakeningLabel.color = new Color(1, .85f, .2f); _awakeningLabel.raycastTarget = false;
            }
            _displayed = s; _awakening.gameObject.SetActive(true); _awakening.SetAmount(s.State.Awakening);
            _awakeningLabel.text = "覚醒 " + s.State.Awakening.ToString("0") + "%\n" + s.Actor.chaFile.parameter.fullname;
        }
        void HideAwakening(ActorState s)
        {
            if (_displayed != s) return;
            _displayed = null;
            if (_awakening != null) _awakening.gameObject.SetActive(false);
            var other = _actors.Find(a => a.State.Doll);
            if (other != null) ShowAwakening(other);
        }
        void DisposeAwakening()
        {
            if (_awakening != null) { _awakening.gameObject.SetActive(false); Destroy(_awakening.gameObject); }
            _awakening = null; _awakeningLabel = null; _displayed = null;
        }
    }
}
