using System.Collections.Generic;
using UnityEngine;

namespace MainGameSubCameraDisplayProbe
{
    public sealed partial class Plugin
    {
        private const int CharactersOnlyDedicatedLayer = 31;
        private const float CharactersOnlyCharacterRescanInterval = 0.5f;
        private const float CharactersOnlyStatsLogInterval = 5f;

        private struct LayerSwap
        {
            public GameObject GameObject;
            public int OriginalLayer;
        }

        private bool _charactersOnlyHooked;
        private float _charactersOnlyCharacterNextRescan;
        private readonly List<GameObject> _charactersOnlyKeepObjects = new List<GameObject>(512);
        private readonly List<LayerSwap> _charactersOnlySwappedThisFrame = new List<LayerSwap>(512);
        private bool _charactersOnlyApplying;
        private int _charactersOnlySavedCullingMask;
        private int _charactersOnlyPreCullCount;
        private int _charactersOnlyPostRenderCount;
        private int _charactersOnlyLastChaControlCount;
        private float _charactersOnlyNextStatsLog;

        private void EnableCharactersOnlyHook()
        {
            if (_charactersOnlyHooked)
                return;

            Camera.onPreCull += OnCharactersOnlyPreCull;
            Camera.onPostRender += OnCharactersOnlyPostRender;
            _charactersOnlyHooked = true;
            _charactersOnlyPreCullCount = 0;
            _charactersOnlyPostRenderCount = 0;
            _charactersOnlyNextStatsLog = Time.unscaledTime + CharactersOnlyStatsLogInterval;
            LogInfo("[characters-only] hook enabled subCamera=" + (_subCamera != null ? _subCamera.name : "null") + " dedicatedLayer=" + CharactersOnlyDedicatedLayer);
        }

        private void DisableCharactersOnlyHook()
        {
            if (!_charactersOnlyHooked)
                return;

            Camera.onPreCull -= OnCharactersOnlyPreCull;
            Camera.onPostRender -= OnCharactersOnlyPostRender;
            _charactersOnlyHooked = false;

            RestoreCharactersOnlySwapped();
            _charactersOnlyApplying = false;
            _charactersOnlyKeepObjects.Clear();
            _charactersOnlyCharacterNextRescan = 0f;
            LogInfo("[characters-only] hook disabled");
        }

        private void OnCharactersOnlyPreCull(Camera cam)
        {
            if (cam == null || cam != _subCamera)
                return;
            if (_settings == null || !_settings.CharactersOnlyMode)
                return;
            if (_charactersOnlyApplying)
            {
                LogWarn("[characters-only] PreCull re-entry detected cam=" + cam.name + " — skipping");
                return;
            }

            float now = Time.unscaledTime;
            if (now >= _charactersOnlyCharacterNextRescan || _charactersOnlyKeepObjects.Count == 0)
            {
                int before = _charactersOnlyKeepObjects.Count;
                RebuildCharactersOnlyKeepObjects();
                _charactersOnlyCharacterNextRescan = now + CharactersOnlyCharacterRescanInterval;
                if (_charactersOnlyKeepObjects.Count != before)
                    LogInfo("[characters-only] keep set rebuilt chaCount=" + _charactersOnlyLastChaControlCount + " renderers=" + _charactersOnlyKeepObjects.Count);
            }

            _charactersOnlySwappedThisFrame.Clear();
            int dedicatedLayer = CharactersOnlyDedicatedLayer;
            for (int i = 0; i < _charactersOnlyKeepObjects.Count; i++)
            {
                GameObject go = _charactersOnlyKeepObjects[i];
                if (go == null)
                    continue;

                int original = go.layer;
                if (original == dedicatedLayer)
                    continue;

                _charactersOnlySwappedThisFrame.Add(new LayerSwap { GameObject = go, OriginalLayer = original });
                go.layer = dedicatedLayer;
            }

            _charactersOnlySavedCullingMask = cam.cullingMask;
            cam.cullingMask = 1 << dedicatedLayer;
            _charactersOnlyApplying = true;
            _charactersOnlyPreCullCount++;

            if (now >= _charactersOnlyNextStatsLog)
            {
                LogInfo("[characters-only] stats interval=" + CharactersOnlyStatsLogInterval + "s preCullCount=" + _charactersOnlyPreCullCount
                    + " postRenderCount=" + _charactersOnlyPostRenderCount
                    + " keepObjects=" + _charactersOnlyKeepObjects.Count
                    + " swappedThisCall=" + _charactersOnlySwappedThisFrame.Count
                    + " savedMask=0x" + _charactersOnlySavedCullingMask.ToString("X8")
                    + " newMask=0x" + cam.cullingMask.ToString("X8"));
                _charactersOnlyPreCullCount = 0;
                _charactersOnlyPostRenderCount = 0;
                _charactersOnlyNextStatsLog = now + CharactersOnlyStatsLogInterval;
            }
        }

        private void OnCharactersOnlyPostRender(Camera cam)
        {
            if (cam == null || cam != _subCamera)
                return;
            if (!_charactersOnlyApplying)
                return;

            cam.cullingMask = _charactersOnlySavedCullingMask;
            RestoreCharactersOnlySwapped();
            _charactersOnlyApplying = false;
            _charactersOnlyPostRenderCount++;
        }

        private void RestoreCharactersOnlySwapped()
        {
            for (int i = 0; i < _charactersOnlySwappedThisFrame.Count; i++)
            {
                LayerSwap swap = _charactersOnlySwappedThisFrame[i];
                if (swap.GameObject != null)
                    swap.GameObject.layer = swap.OriginalLayer;
            }
            _charactersOnlySwappedThisFrame.Clear();
        }

        private void RebuildCharactersOnlyKeepObjects()
        {
            _charactersOnlyKeepObjects.Clear();
            ChaControl[] characters = Object.FindObjectsOfType<ChaControl>();
            _charactersOnlyLastChaControlCount = characters.Length;
            for (int i = 0; i < characters.Length; i++)
            {
                ChaControl cha = characters[i];
                if (cha == null)
                    continue;

                Renderer[] charRenderers = cha.GetComponentsInChildren<Renderer>(true);
                for (int j = 0; j < charRenderers.Length; j++)
                {
                    Renderer r = charRenderers[j];
                    if (r == null)
                        continue;
                    GameObject go = r.gameObject;
                    if (go != null)
                        _charactersOnlyKeepObjects.Add(go);
                }
            }
        }

        private void SyncCharactersOnlyHook()
        {
            if (_settings == null)
                return;

            if (_charactersOnlyApplying && _charactersOnlySwappedThisFrame.Count > 0)
            {
                if (_subCamera != null)
                    _subCamera.cullingMask = _charactersOnlySavedCullingMask;
                RestoreCharactersOnlySwapped();
                _charactersOnlyApplying = false;
            }

            bool shouldHook = _settings.CharactersOnlyMode && _settings.Enabled && _subCamera != null;
            if (shouldHook)
                EnableCharactersOnlyHook();
            else
                DisableCharactersOnlyHook();

            if (_subCamera != null)
            {
                Color desiredBg = (_settings.CharactersOnlyMode && _settings.Enabled) ? Color.clear : Color.black;
                if (_subCamera.backgroundColor != desiredBg)
                    _subCamera.backgroundColor = desiredBg;
            }
        }
    }
}
