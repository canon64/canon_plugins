using System;
using System.Collections.Generic;
using System.Linq;

namespace MainGameAiShoujoMode
{
    internal static class HotelPoseCatalogDiagnostics
    {
        internal static string Identity(HotelRoomSelection value) => value == null ? "none"
            : "mode=" + value.mode + " id=" + value.id + " name=" + value.name + " bundle=" + value.bundle + " asset=" + value.asset;
        internal static void Write(Func<bool> enabled, Action<string, Func<string>> sink, string stage,
            Func<IEnumerable<HotelRoomSelection>> candidates, HotelRoomSelection saved, Func<string> context, bool details)
        {
            if (enabled?.Invoke() != true || sink == null) return;
            sink("pose-catalog." + stage, () =>
            {
                var records = candidates().Where(value => value != null).ToArray();
                string header = context() + " saved={" + Identity(saved) + "} count=" + records.Length
                    + " modes=[" + string.Join(";", records.GroupBy(value => value.mode).Select(group => group.Key + ":" + group.Count()))
                    + "] clones=" + records.Count(value => (value.name ?? "").Contains("【クローン】"))
                    + " exact=" + records.Count(value => saved?.Matches(value) == true);
                if (!details || saved == null) return header;
                var nearby = records.Where(value => value.id == saved.id || value.name == saved.name || (value.name ?? "").Contains("【クローン】"));
                return header + " nearby=[" + string.Join(";", nearby.Select(value => "{" + Identity(value)
                    + " match(mode/id/name/bundle/asset)=" + (value.mode == saved.mode) + "/" + (value.id == saved.id) + "/"
                    + (value.name == saved.name) + "/" + (value.bundle == saved.bundle) + "/" + (value.asset == saved.asset) + "}")) + "]";
            });
        }
    }
}
