using System;
using BepInEx.Bootstrap;
namespace CanonPlugins.Shared
{
    internal static class VmdRhythmSource
    {
        private delegate bool ReadSource(out string path, out double time, out bool playing);
        private static ReadSource _read;
        internal static void Prepare()
        {
            _read = null;
            if (Chainloader.PluginInfos.TryGetValue("com.kks.maingame.vmdplay", out var plugin))
            {
                var method = plugin.Instance.GetType().GetMethod("TryGetRhythmSource");
                if (method != null) _read = (ReadSource)Delegate.CreateDelegate(typeof(ReadSource), method);
            }
        }
        internal static bool TryRead(out string path, out double time, out bool playing)
        { path = null; time = 0; playing = false; return _read != null && _read(out path, out time, out playing); }
    }
}
