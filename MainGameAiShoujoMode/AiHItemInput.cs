using System;
using MainGameUiInputCapture;
using MapEnvironmentRuntime;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MainGameAiShoujoMode
{
    // Native uGUI continues to handle scrolling/dragging. No Update or polling.
    internal sealed class AiHItemInput : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler,
        IPointerUpHandler
    {
        private string _owner;
        private Vector3[] _corners;
        private Parameters _settings;
        internal void Initialize(string suffix, Parameters settings)
        { _settings = settings; _corners = new Vector3[settings.I("hItems.cornerCount")]; _owner = Plugin.Guid + ".hitems." + suffix; Refresh(); }
        internal void Refresh()
        {
            if (_owner == null || !isActiveAndEnabled) return;
            var rect = transform as RectTransform;
            if (rect == null) return;
            var canvas = GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            rect.GetWorldCorners(_corners);
            var min = RectTransformUtility.WorldToScreenPoint(camera, _corners[_settings.I("math.zero")]);
            var max = RectTransformUtility.WorldToScreenPoint(camera, _corners[_settings.I("math.two")]);
            AiModeNativeUiCapture.Apply(_owner, new Rect(min.x, Screen.height - max.y, max.x - min.x, max.y - min.y));
        }
        private void Clear()
        {
            if (_owner == null) return;
            UiInputCaptureApi.Sync(_owner, "pointer", false);
            UiInputCaptureApi.Sync(_owner, "drag", false);
            UiInputCaptureApi.ClearInputBlockRects(_owner);
            UiInputCaptureApi.ClearBlockRects(_owner);
            UiInputCaptureApi.SetIdleCursorUnlock(_owner, false);
        }
        private void OnEnable() => Refresh();
        private void OnDisable() => Clear();
        private void OnDestroy() => Clear();
        private void OnRectTransformDimensionsChange() => Refresh();
        private void OnApplicationFocus(bool focus) { if (focus) Refresh(); else Clear(); }
        public void OnPointerEnter(PointerEventData data) => Refresh();
        public void OnPointerDown(PointerEventData data) { Refresh(); UiInputCaptureApi.Sync(_owner, "pointer", true); }
        public void OnPointerUp(PointerEventData data) => UiInputCaptureApi.Sync(_owner, "pointer", false);
        public void OnBeginDrag(PointerEventData data) => UiInputCaptureApi.Sync(_owner, "drag", true);
        public void OnEndDrag(PointerEventData data) => UiInputCaptureApi.Sync(_owner, "drag", false);
    }

    internal sealed class AiHItemPointer : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        internal AiHItemInput Owner;
        public void OnPointerDown(PointerEventData data) => Owner.OnPointerDown(data);
        public void OnPointerUp(PointerEventData data) => Owner.OnPointerUp(data);
    }
    // Only attach on the actual native ScrollRect/Scrollbar; rows must not steal drag handling.
    internal sealed class AiHItemDrag : MonoBehaviour, IBeginDragHandler, IEndDragHandler
    {
        internal AiHItemInput Owner;
        public void OnBeginDrag(PointerEventData data) => Owner.OnBeginDrag(data);
        public void OnEndDrag(PointerEventData data) => Owner.OnEndDrag(data);
    }

    internal sealed class AiHItemClick : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IPointerExitHandler
    {
        internal Action Delete;
        private bool _right;
        public void OnPointerDown(PointerEventData data)
        { _right = data.button == PointerEventData.InputButton.Right; }
        public void OnPointerUp(PointerEventData data)
        {
            bool invoke = _right && data.button == PointerEventData.InputButton.Right && !data.dragging;
            _right = false;
            if (invoke) { Delete?.Invoke(); data.Use(); }
        }
        public void OnPointerExit(PointerEventData data) { _right = false; }
        private void OnDisable() { _right = false; }
        private void OnApplicationFocus(bool focus) { if (!focus) _right = false; }
    }
}
