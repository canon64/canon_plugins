using System;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    internal sealed class NativeActionUiScope : IDisposable
    {
        private readonly GameObject _minimap;
        private readonly bool _previousActive;
        private bool _disposed;
        internal NativeActionUiScope(ActionScene action)
        {
            _minimap = action.miniMap != null ? action.miniMap.gameObject : null;
            if (_minimap == null) return;
            _previousActive = _minimap.activeSelf;
            // Keep Minimap.isVisible's logical state: native input uses it independently of rendering.
            _minimap.SetActive(false);
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            if (_minimap != null) _minimap.SetActive(_previousActive);
        }
    }
}
