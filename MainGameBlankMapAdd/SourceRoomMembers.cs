using System;
using System.Collections.Generic;
using UnityEngine;

namespace MainGameBlankMapAdd
{
    // No Update callbacks: rebuild only after a hierarchy change or an explicit component edit.
    internal sealed class SourceRoomMembers : IDisposable
    {
        private GameObject _root;
        private bool _dirty;
        private readonly List<Transform> _transforms = new List<Transform>();
        private readonly List<SourceRoomHierarchyNotice> _notices = new List<SourceRoomHierarchyNotice>();
        internal readonly List<Renderer> Renderers = new List<Renderer>();
        internal readonly List<Light> Lights = new List<Light>();

        internal void Invalidate() { _dirty = true; }

        internal void Ensure(GameObject root)
        {
            if (_root != root)
            {
                Dispose();
                _root = root;
                _dirty = true;
            }
            if (root == null) { Dispose(); return; }
            if (!_dirty) return;

            // Existing notices are reused. Removed subtrees must not keep this cache alive.
            foreach (var notice in _notices)
                if (notice != null && ReferenceEquals(notice.Owner, this)) notice.Owner = null;
            root.GetComponentsInChildren(true, _transforms);
            foreach (Transform node in _transforms)
            {
                var notice = node.GetComponent<SourceRoomHierarchyNotice>();
                if (notice == null) notice = node.gameObject.AddComponent<SourceRoomHierarchyNotice>();
                notice.Owner = this;
            }
            _notices.Clear();
            foreach (Transform node in _transforms)
                _notices.Add(node.GetComponent<SourceRoomHierarchyNotice>());
            root.GetComponentsInChildren(true, Renderers);
            root.GetComponentsInChildren(true, Lights);
            _dirty = false;
        }

        public void Dispose()
        {
            foreach (var notice in _notices)
                if (notice != null && ReferenceEquals(notice.Owner, this))
                    notice.Owner = null;
            // Leave inert notices reusable until their GameObjects die. Destroy is deferred in
            // Unity; destroying here could remove a notice reused by another overlay this frame.
            _notices.Clear();
            _transforms.Clear();
            Renderers.Clear();
            Lights.Clear();
            _root = null;
            _dirty = false;
        }
    }

    internal sealed class SourceRoomHierarchyNotice : MonoBehaviour
    {
        internal SourceRoomMembers Owner;
        private void OnTransformChildrenChanged() { Owner?.Invalidate(); }
        private void OnTransformParentChanged() { Owner?.Invalidate(); }
        private void OnEnable() { Owner?.Invalidate(); }
        private void OnDestroy() { Owner?.Invalidate(); Owner = null; }
    }
}
