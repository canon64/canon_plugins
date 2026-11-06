using System;
using System.IO;
using UnityEngine;

namespace MainGameSamabakeMotion
{
    [Serializable] public sealed class MotionCatalog { public MotionEntry[] entries; public int bundles; public int skipped; }
    [Serializable] public sealed class MotionEntry
    {
        public string key, kind, title;
        public MotionActor female, male;
    }
    [Serializable] public sealed class MotionActor
    {
        public string file, controller;
        public MotionState[] states;
    }
    [Serializable] public sealed class MotionState
    {
        public string name, label;
        public bool corrective, pattern, composite;
    }
    [Serializable] public sealed class PlaybackSettings { public float speed = 1f; public float pattern = 0.5f; }
    internal static class CatalogStore
    {
        internal static T Read<T>(string directory, string file)
            => JsonUtility.FromJson<T>(File.ReadAllText(Path.Combine(directory, file), System.Text.Encoding.UTF8));
    }
}
