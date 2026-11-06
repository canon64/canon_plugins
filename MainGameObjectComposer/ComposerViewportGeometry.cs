using System;
using UnityEngine;

namespace MainGameObjectComposer
{
    internal static class ComposerViewportGeometry
    {
        internal static Rect Body(Rect allocation, float width, float height)
        {
            // GUILayout returns a placeholder during Layout. Only use its eventual position;
            // all nested areas must receive the real available size on every event.
            return new Rect(allocation.x, allocation.y, Math.Max(320f, width), Math.Max(100f, height - 34f));
        }
    }
}
