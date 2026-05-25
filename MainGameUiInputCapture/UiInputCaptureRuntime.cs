using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MainGameUiInputCapture
{
    internal sealed class UiInputCaptureRuntime
    {
        private readonly Plugin _plugin;
        private readonly HashSet<string> _activeTokens = new HashSet<string>(System.StringComparer.Ordinal);
        private readonly HashSet<string> _idleUnlockOwners = new HashSet<string>(System.StringComparer.Ordinal);
        private readonly List<string> _scratchTokens = new List<string>();

        // uGUI クリック貫通防止ブロッカー。owner ごとに IMGUI 矩形群を保持し、
        // 透明 Image (Canvas+GraphicRaycaster) で覆って uGUI クリックを吸収する。
        private readonly Dictionary<string, List<Rect>> _blockRectsByOwner =
            new Dictionary<string, List<Rect>>(System.StringComparer.Ordinal);
        private readonly Dictionary<string, List<Rect>> _inputBlockRectsByOwner =
            new Dictionary<string, List<Rect>>(System.StringComparer.Ordinal);
        private GameObject _blockerRoot;
        private readonly List<Image> _blockerImages = new List<Image>();

        private bool _captureApplied;
        private bool _hasPrevCtrlCameraCursorLock;
        private bool _prevCtrlCameraCursorLock;
        private bool _hasPrevCtrlCameraNoCtrl;
        private BaseCameraControl_Ver2.NoCtrlFunc _prevCtrlCameraNoCtrl;
        private bool _hasPrevCtrlCameraKeyCondition;
        private BaseCameraControl_Ver2.NoCtrlFunc _prevCtrlCameraKeyCondition;
        private bool _hasPrevGameCursorLock;
        private bool _prevGameCursorLock;
        private bool _hasPrevActionSceneCursorLock;
        private bool _prevActionSceneCursorLock;

        private HFlag _cachedHFlag;
        private CameraControl_Ver2 _cachedCtrlCamera;

        internal UiInputCaptureRuntime(Plugin plugin)
        {
            _plugin = plugin;
        }

        internal bool IsAnyActive => _activeTokens.Count > 0;

        internal void Sync(string ownerKey, string sourceKey, bool active)
        {
            if (!TryNormalizeKey(ownerKey, out string owner)
                || !TryNormalizeKey(sourceKey, out string source))
                return;

            string token = BuildToken(owner, source);
            if (active)
            {
                bool added = _activeTokens.Add(token);
                if (added)
                    _plugin.LogDebugForOwner(owner, $"capture token add owner={owner} source={source} active={_activeTokens.Count}");
                ApplyCapture("sync:" + token);
            }
            else
            {
                bool removed = _activeTokens.Remove(token);
                if (removed)
                    _plugin.LogDebugForOwner(owner, $"capture token remove owner={owner} source={source} active={_activeTokens.Count}");
                if (_activeTokens.Count == 0)
                    Restore("sync-end:" + token);
            }
        }

        internal bool Begin(string ownerKey, string sourceKey)
        {
            if (!TryNormalizeKey(ownerKey, out string owner)
                || !TryNormalizeKey(sourceKey, out string source))
            {
                _plugin.LogWarn("Begin rejected: owner/source empty");
                return false;
            }

            string token = BuildToken(owner, source);
            bool added = _activeTokens.Add(token);
            if (added)
            {
                _plugin.LogDebugForOwner(owner, $"capture token add owner={owner} source={source} active={_activeTokens.Count}");
            }

            ApplyCapture("begin:" + token);
            return added;
        }

        internal bool Tick(string ownerKey, string sourceKey)
        {
            if (!TryNormalizeKey(ownerKey, out string owner)
                || !TryNormalizeKey(sourceKey, out string source))
            {
                return false;
            }

            string token = BuildToken(owner, source);
            if (!_activeTokens.Contains(token))
            {
                return false;
            }

            ApplyCapture("tick:" + token);
            return true;
        }

        internal bool End(string ownerKey, string sourceKey)
        {
            if (!TryNormalizeKey(ownerKey, out string owner)
                || !TryNormalizeKey(sourceKey, out string source))
            {
                return false;
            }

            string token = BuildToken(owner, source);
            bool removed = _activeTokens.Remove(token);
            if (removed)
            {
                _plugin.LogDebugForOwner(owner, $"capture token remove owner={owner} source={source} active={_activeTokens.Count}");
            }

            if (_activeTokens.Count == 0)
            {
                Restore("end:" + token);
            }

            return removed;
        }

        internal int EndOwner(string ownerKey)
        {
            if (!TryNormalizeKey(ownerKey, out string owner))
            {
                return 0;
            }

            string prefix = owner + "::";
            _scratchTokens.Clear();
            foreach (string token in _activeTokens)
            {
                if (token.StartsWith(prefix, System.StringComparison.Ordinal))
                {
                    _scratchTokens.Add(token);
                }
            }

            int removed = 0;
            for (int i = 0; i < _scratchTokens.Count; i++)
            {
                if (_activeTokens.Remove(_scratchTokens[i]))
                    removed++;
            }

            bool idleRemoved = _idleUnlockOwners.Remove(owner);
            if (removed > 0 || idleRemoved)
            {
                _plugin.LogDebugForOwner(owner, $"capture owner clear owner={owner} removed={removed} idleRemoved={idleRemoved}");
            }

            if (_blockRectsByOwner.Remove(owner))
            {
                UpdateBlocker();
            }
            _inputBlockRectsByOwner.Remove(owner);

            if (_activeTokens.Count == 0)
            {
                Restore("end-owner:" + owner);
            }

            return removed;
        }

        internal bool SetIdleCursorUnlock(string ownerKey, bool enabled)
        {
            if (!TryNormalizeKey(ownerKey, out string owner))
            {
                return false;
            }

            bool changed = enabled
                ? _idleUnlockOwners.Add(owner)
                : _idleUnlockOwners.Remove(owner);

            if (changed)
            {
                _plugin.LogDebugForOwner(owner, $"idle unlock {(enabled ? "ON" : "OFF")} owner={owner} total={_idleUnlockOwners.Count}");
            }

            if (!enabled
                && _idleUnlockOwners.Count == 0
                && _activeTokens.Count == 0)
            {
                Restore("idle-off:" + owner);
            }

            if (enabled)
            {
                ApplyIdleCursorUnlock();
            }

            return changed;
        }

        /// <summary>
        /// owner の uGUI ブロック矩形群を置き換える。矩形は IMGUI 座標（左上原点）。
        /// null/空 を渡すと owner のブロックを解除する。
        /// </summary>
        internal void SetBlockRects(string ownerKey, IList<Rect> rects)
        {
            if (!TryNormalizeKey(ownerKey, out string owner))
            {
                return;
            }

            if (rects == null || rects.Count == 0)
            {
                if (_blockRectsByOwner.Remove(owner))
                {
                    UpdateBlocker();
                }
                return;
            }

            if (!_blockRectsByOwner.TryGetValue(owner, out List<Rect> list))
            {
                list = new List<Rect>(rects.Count);
                _blockRectsByOwner[owner] = list;
            }
            list.Clear();
            for (int i = 0; i < rects.Count; i++)
            {
                list.Add(rects[i]);
            }
            UpdateBlocker();
        }

        /// <summary>
        /// owner の入力ブロック矩形群を置き換える。矩形は IMGUI 座標（左上原点）。
        /// uGUI クリック吸収用ブロッカーも同じ矩形で更新する。
        /// </summary>
        internal void SetInputBlockRects(string ownerKey, IList<Rect> rects)
        {
            if (!TryNormalizeKey(ownerKey, out string owner))
            {
                return;
            }

            if (rects == null || rects.Count == 0)
            {
                _inputBlockRectsByOwner.Remove(owner);
                SetBlockRects(owner, null);
                return;
            }

            if (!_inputBlockRectsByOwner.TryGetValue(owner, out List<Rect> list))
            {
                list = new List<Rect>(rects.Count);
                _inputBlockRectsByOwner[owner] = list;
            }
            list.Clear();
            for (int i = 0; i < rects.Count; i++)
            {
                list.Add(rects[i]);
            }

            SetBlockRects(owner, rects);
        }

        private void EnsureBlocker()
        {
            if (_blockerRoot != null)
            {
                return;
            }

            _blockerRoot = new GameObject("UiInputCaptureBlocker");
            Object.DontDestroyOnLoad(_blockerRoot);

            Canvas canvas = _blockerRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 29999;

            _blockerRoot.AddComponent<GraphicRaycaster>();
        }

        private Image CreateBlockerImage()
        {
            var go = new GameObject("BlockerImage");
            go.transform.SetParent(_blockerRoot.transform, false);
            var image = go.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;
            return image;
        }

        /// <summary>
        /// 全 owner の矩形を集約し、透明 Image プールに割り当てて配置する。
        /// 矩形ゼロならブロッカー全体を非アクティブにする。
        /// </summary>
        private void UpdateBlocker()
        {
            int needed = 0;
            foreach (KeyValuePair<string, List<Rect>> kv in _blockRectsByOwner)
            {
                if (kv.Value != null)
                {
                    needed += kv.Value.Count;
                }
            }

            if (needed == 0)
            {
                if (_blockerRoot != null && _blockerRoot.activeSelf)
                {
                    _blockerRoot.SetActive(false);
                }
                return;
            }

            EnsureBlocker();
            if (_blockerRoot == null)
            {
                return;
            }

            while (_blockerImages.Count < needed)
            {
                _blockerImages.Add(CreateBlockerImage());
            }

            float screenW = Screen.width;
            float screenH = Screen.height;
            int idx = 0;
            foreach (KeyValuePair<string, List<Rect>> kv in _blockRectsByOwner)
            {
                List<Rect> list = kv.Value;
                if (list == null)
                {
                    continue;
                }
                for (int i = 0; i < list.Count; i++)
                {
                    Rect r = list[i];
                    Image img = _blockerImages[idx++];
                    RectTransform rt = img.rectTransform;

                    // IMGUI座標(左上原点) → ScreenSpaceOverlay(左下原点) に変換
                    float uiX = r.x;
                    float uiY = screenH - r.yMax;
                    rt.anchorMin = new Vector2(uiX / screenW, uiY / screenH);
                    rt.anchorMax = new Vector2((uiX + r.width) / screenW, (uiY + r.height) / screenH);
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;

                    if (!img.gameObject.activeSelf)
                    {
                        img.gameObject.SetActive(true);
                    }
                }
            }

            for (int i = idx; i < _blockerImages.Count; i++)
            {
                if (_blockerImages[i].gameObject.activeSelf)
                {
                    _blockerImages[i].gameObject.SetActive(false);
                }
            }

            if (!_blockerRoot.activeSelf)
            {
                _blockerRoot.SetActive(true);
            }
        }

        internal bool IsOwnerActive(string ownerKey)
        {
            if (!TryNormalizeKey(ownerKey, out string owner))
            {
                return false;
            }

            string prefix = owner + "::";
            foreach (string token in _activeTokens)
            {
                if (token.StartsWith(prefix, System.StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        internal bool IsMouseInputBlocked()
        {
            return TryGetInputBlockOwnerUnderMouse(out _);
        }

        internal bool IsMouseInputBlockedForOwner(string ownerKey)
        {
            if (!TryNormalizeKey(ownerKey, out string owner))
            {
                return IsMouseInputBlocked();
            }

            if (_inputBlockRectsByOwner.Count == 0)
            {
                return false;
            }

            Vector2 guiMouse = GetGuiMousePosition();
            foreach (KeyValuePair<string, List<Rect>> kv in _inputBlockRectsByOwner)
            {
                if (string.Equals(kv.Key, owner, System.StringComparison.Ordinal))
                {
                    continue;
                }

                if (ContainsGuiMouse(kv.Value, guiMouse))
                {
                    return true;
                }
            }

            return false;
        }

        internal string GetStateSummary()
        {
            return "activeTokens=" + _activeTokens.Count
                + ", idleUnlockOwners=" + _idleUnlockOwners.Count
                + ", inputBlockOwners=" + _inputBlockRectsByOwner.Count
                + ", captureApplied=" + _captureApplied
                + ", hasSavedState=" + HasSavedState();
        }

        internal void FrameUpdate()
        {
            // ブロッカーはキャプチャ状態と独立。解像度/ウィンドウ移動追従のため毎フレーム再配置。
            UpdateBlocker();

            if (TryGetInputBlockOwnerUnderMouse(out string blockOwner))
            {
                ApplyCapture("input-block:" + blockOwner);
                return;
            }

            if (_activeTokens.Count > 0)
            {
                ApplyCapture("frame-active");
                return;
            }

            if (_captureApplied || HasSavedState())
            {
                Restore("frame-inactive");
            }

            if (_idleUnlockOwners.Count > 0)
            {
                ApplyIdleCursorUnlock();
            }
        }

        internal void ReleaseAll(string reason)
        {
            _activeTokens.Clear();
            _idleUnlockOwners.Clear();
            _inputBlockRectsByOwner.Clear();
            Restore(reason);
            // 状態に関わらずカーソルを必ず解放（Restore が早期 return した場合の保険）
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // ブロッカー破棄（Image は root の子なので root 破棄で一緒に消える）
            _blockRectsByOwner.Clear();
            _blockerImages.Clear();
            if (_blockerRoot != null)
            {
                Object.Destroy(_blockerRoot);
                _blockerRoot = null;
            }
        }

        private void ApplyCapture(string reason)
        {
            CameraControl_Ver2 ctrl = ResolveCtrlCamera();
            if (ctrl != null)
            {
                if (!_hasPrevCtrlCameraCursorLock)
                {
                    _prevCtrlCameraCursorLock = ctrl.isCursorLock;
                    _hasPrevCtrlCameraCursorLock = true;
                }
                if (!_hasPrevCtrlCameraNoCtrl)
                {
                    _prevCtrlCameraNoCtrl = ctrl.NoCtrlCondition;
                    _hasPrevCtrlCameraNoCtrl = true;
                }
                if (!_hasPrevCtrlCameraKeyCondition)
                {
                    _prevCtrlCameraKeyCondition = ctrl.KeyCondition;
                    _hasPrevCtrlCameraKeyCondition = true;
                }

                ctrl.isCursorLock = false;
                ctrl.NoCtrlCondition = AlwaysTrue;
                ctrl.KeyCondition = AlwaysFalse;
                GlobalMethod.SetCameraMoveFlag(ctrl, _bPlay: false);
            }

            GameCursor gameCursor = Singleton<GameCursor>.Instance;
            if (gameCursor != null)
            {
                if (!_hasPrevGameCursorLock)
                {
                    _prevGameCursorLock = GameCursor.isLock;
                    _hasPrevGameCursorLock = true;
                }

                gameCursor.SetCursorLock(false);
                GameCursor.isDraw = true;
            }

            ActionScene actionScene = SingletonInitializer<ActionScene>.instance;
            if (actionScene != null)
            {
                if (!_hasPrevActionSceneCursorLock)
                {
                    _prevActionSceneCursorLock = actionScene.isCursorLock;
                    _hasPrevActionSceneCursorLock = true;
                }

                actionScene.SetCursorLock(false);
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (!_captureApplied)
            {
                _captureApplied = true;
                if (ShouldLogCaptureTransition(reason))
                {
                    _plugin.LogDebugRaw("capture ON (" + reason + ")");
                    if (_plugin.LogStateOnTransition)
                        _plugin.LogDebugRaw("capture state: " + GetStateSummary());
                }
            }
        }

        private void Restore(string reason)
        {
            if (!_captureApplied && !HasSavedState())
            {
                if (_idleUnlockOwners.Count > 0)
                {
                    ApplyIdleCursorUnlock();
                }
                return;
            }

            CameraControl_Ver2 ctrl = ResolveCtrlCamera();
            if (ctrl != null)
            {
                if (_hasPrevCtrlCameraCursorLock)
                    ctrl.isCursorLock = _prevCtrlCameraCursorLock;

                if (_hasPrevCtrlCameraNoCtrl)
                    ctrl.NoCtrlCondition = _prevCtrlCameraNoCtrl;
                else if (IsNoCtrlFunc(ctrl.NoCtrlCondition, AlwaysTrue))
                    ctrl.NoCtrlCondition = AlwaysFalse;

                if (_hasPrevCtrlCameraKeyCondition)
                    ctrl.KeyCondition = _prevCtrlCameraKeyCondition;
                else if (IsNoCtrlFunc(ctrl.KeyCondition, AlwaysFalse))
                    ctrl.KeyCondition = AlwaysTrue;

                GlobalMethod.SetCameraMoveFlag(ctrl, _bPlay: true);
            }

            GameCursor gameCursor = Singleton<GameCursor>.Instance;
            if (gameCursor != null && _hasPrevGameCursorLock)
            {
                gameCursor.SetCursorLock(_prevGameCursorLock);
                GameCursor.isDraw = !_prevGameCursorLock;
            }

            ActionScene actionScene = SingletonInitializer<ActionScene>.instance;
            if (actionScene != null && _hasPrevActionSceneCursorLock)
            {
                actionScene.SetCursorLock(_prevActionSceneCursorLock);
            }

            bool restoreLock = (_hasPrevCtrlCameraCursorLock && _prevCtrlCameraCursorLock)
                || (_hasPrevGameCursorLock && _prevGameCursorLock)
                || (_hasPrevActionSceneCursorLock && _prevActionSceneCursorLock);

            if (_idleUnlockOwners.Count > 0)
            {
                restoreLock = false;
            }

            Cursor.lockState = restoreLock ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !restoreLock;

            _captureApplied = false;
            _hasPrevCtrlCameraCursorLock = false;
            _hasPrevCtrlCameraNoCtrl = false;
            _hasPrevCtrlCameraKeyCondition = false;
            _hasPrevGameCursorLock = false;
            _hasPrevActionSceneCursorLock = false;

            if (ShouldLogCaptureTransition(reason))
            {
                _plugin.LogDebugRaw("capture OFF (" + reason + ")");
                if (_plugin.LogStateOnTransition)
                    _plugin.LogDebugRaw("capture state: " + GetStateSummary());
            }

            if (_idleUnlockOwners.Count > 0)
            {
                ApplyIdleCursorUnlock();
            }
        }

        private void ApplyIdleCursorUnlock()
        {
            GameCursor gameCursor = Singleton<GameCursor>.Instance;
            if (gameCursor != null)
            {
                GameCursor.isDraw = true;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private bool TryGetInputBlockOwnerUnderMouse(out string owner)
        {
            owner = string.Empty;
            if (_inputBlockRectsByOwner.Count == 0)
            {
                return false;
            }

            Vector2 guiMouse = GetGuiMousePosition();
            foreach (KeyValuePair<string, List<Rect>> kv in _inputBlockRectsByOwner)
            {
                if (ContainsGuiMouse(kv.Value, guiMouse))
                {
                    owner = kv.Key;
                    return true;
                }
            }

            return false;
        }

        private static Vector2 GetGuiMousePosition()
        {
            Vector3 mouse = Input.mousePosition;
            return new Vector2(mouse.x, Screen.height - mouse.y);
        }

        private static bool ContainsGuiMouse(List<Rect> rects, Vector2 guiMouse)
        {
            if (rects == null)
            {
                return false;
            }

            for (int i = 0; i < rects.Count; i++)
            {
                Rect r = rects[i];
                if (r.width <= 0f || r.height <= 0f)
                {
                    continue;
                }

                if (r.Contains(guiMouse))
                {
                    return true;
                }
            }

            return false;
        }

        private CameraControl_Ver2 ResolveCtrlCamera()
        {
            if (_cachedCtrlCamera != null)
            {
                return _cachedCtrlCamera;
            }

            if (_cachedHFlag == null)
            {
                _cachedHFlag = Object.FindObjectOfType<HFlag>();
            }

            if (_cachedHFlag != null)
            {
                _cachedCtrlCamera = _cachedHFlag.ctrlCamera;
            }

            return _cachedCtrlCamera;
        }

        private bool HasSavedState()
        {
            return _hasPrevCtrlCameraCursorLock
                || _hasPrevCtrlCameraNoCtrl
                || _hasPrevCtrlCameraKeyCondition
                || _hasPrevGameCursorLock
                || _hasPrevActionSceneCursorLock;
        }

        private static bool TryNormalizeKey(string raw, out string normalized)
        {
            normalized = raw == null ? string.Empty : raw.Trim();
            return normalized.Length > 0;
        }

        private static string BuildToken(string owner, string source)
        {
            return owner + "::" + source;
        }

        private static bool AlwaysTrue()
        {
            return true;
        }

        private static bool AlwaysFalse()
        {
            return false;
        }

        private static bool IsNoCtrlFunc(BaseCameraControl_Ver2.NoCtrlFunc current, BaseCameraControl_Ver2.NoCtrlFunc expected)
        {
            if (current == null || expected == null) return false;
            return current.Method == expected.Method && current.Target == expected.Target;
        }

        private bool ShouldLogCaptureTransition(string reason)
        {
            if (TryExtractOwnerFromReason(reason, out string owner))
                return _plugin.IsOwnerDebugEnabled(owner);

            if (HasAnyDebugEnabledActiveOwner())
                return true;

            if (HasAnyDebugEnabledIdleUnlockOwner())
                return true;

            return false;
        }

        private bool HasAnyDebugEnabledActiveOwner()
        {
            foreach (string token in _activeTokens)
            {
                if (!TryExtractOwnerFromToken(token, out string owner))
                    continue;

                if (_plugin.IsOwnerDebugEnabled(owner))
                    return true;
            }

            return false;
        }

        private bool HasAnyDebugEnabledIdleUnlockOwner()
        {
            foreach (string owner in _idleUnlockOwners)
            {
                if (_plugin.IsOwnerDebugEnabled(owner))
                    return true;
            }

            return false;
        }

        private static bool TryExtractOwnerFromReason(string reason, out string owner)
        {
            owner = string.Empty;
            if (string.IsNullOrEmpty(reason))
                return false;

            int idx = reason.IndexOf(':');
            if (idx < 0 || idx + 1 >= reason.Length)
                return false;

            string kind = reason.Substring(0, idx);
            string payload = reason.Substring(idx + 1);
            if (!TryNormalizeKey(payload, out string normalizedPayload))
                return false;

            switch (kind)
            {
                case "sync":
                case "sync-end":
                case "begin":
                case "tick":
                case "end":
                    return TryExtractOwnerFromToken(normalizedPayload, out owner);

                case "end-owner":
                case "idle-off":
                    owner = normalizedPayload;
                    return true;

                default:
                    return TryExtractOwnerFromToken(normalizedPayload, out owner);
            }
        }

        private static bool TryExtractOwnerFromToken(string token, out string owner)
        {
            owner = string.Empty;
            if (string.IsNullOrEmpty(token))
                return false;

            int sep = token.IndexOf("::", System.StringComparison.Ordinal);
            if (sep <= 0)
                return false;

            return TryNormalizeKey(token.Substring(0, sep), out owner);
        }
    }
}
