using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MainGameAiShoujoMode.Conversation
{
    // Only ScenarioData records are loaded; no Game singleton, ADVScene or TalkScene is created.
    internal sealed class KksAdvCatalog : KksAdvCoreHost.IAssets
    {
        private readonly Dictionary<Tuple<string, string>, KksAdvScript> _cache = new Dictionary<Tuple<string, string>, KksAdvScript>();
        private Dictionary<string, KksAdvScript> _parameters;
        internal KksAdvScript Load(string bundle, string asset, string loadBundle)
        {
            bundle = ResolveBundle(bundle, loadBundle);
            var key = Tuple.Create(bundle, asset);
            if (_cache.TryGetValue(key, out var cached)) return cached;
            bool requested = false;
            try
            {
                var operation = AssetBundleManager.LoadAsset(bundle, asset, typeof(ADV.ScenarioData));
                requested = operation != null;
                if (operation == null || operation.IsEmpty()) throw new FileNotFoundException("KKS ADV asset is unavailable: " + bundle + "/" + asset);
                var source = operation.GetAsset<ADV.ScenarioData>();
                if (source == null) throw new FormatException("KKS ADV asset has the wrong type: " + bundle + "/" + asset);
                var result = Copy(bundle, asset, source); _cache.Add(key, result); return result;
            }
            finally { if (requested) AssetBundleManager.UnloadAssetBundle(bundle, isUnloadForceRefCount: true); }
        }
        private static string ResolveBundle(string bundle, string loadBundle)
        {
            if (string.IsNullOrEmpty(bundle)) bundle = loadBundle;
            if (string.IsNullOrEmpty(bundle)) throw new InvalidOperationException("ADV Open has no current bundle.");
            return AssetBundleCheck.IsFile(bundle) ? bundle : ADV.Program.ScenarioBundle(bundle);
        }
        string KksAdvCoreHost.IAssets.ResolveBundle(string bundle, string loadBundle) => ResolveBundle(bundle, loadBundle);
        KksAdvScript KksAdvCoreHost.IAssets.Load(string bundle, string asset, string loadBundle) => Load(bundle, asset, loadBundle);
        bool KksAdvCoreHost.IAssets.TryParameter(string name, out KksAdvScript script) => TryParameter(name, out script);
        internal bool TryParameter(string name, out KksAdvScript script)
        {
            if (_parameters == null)
            {
                var parameters = new Dictionary<string, KksAdvScript>(StringComparer.Ordinal);
                var bundles = CommonLib.GetAssetBundleNameListFromPath("adv/scenario/common_param/", subdirCheck: true);
                bundles.Sort();
                foreach (var bundle in bundles)
                {
                    bool requested = false;
                    try
                    {
                        var operation = AssetBundleManager.LoadAllAsset(bundle, typeof(ADV.ScenarioData));
                        requested = operation != null;
                        if (operation == null || operation.IsEmpty()) throw new FileNotFoundException("KKS ADV parameter bundle is unavailable: " + bundle);
                        foreach (var source in operation.GetAllAssets<ADV.ScenarioData>()) parameters[source.name] = Copy(bundle, source.name, source);
                    }
                    finally { if (requested) AssetBundleManager.UnloadAssetBundle(bundle, isUnloadForceRefCount: true); }
                }
                // Publish only after every override bundle has been copied successfully.
                _parameters = parameters;
            }
            return _parameters.TryGetValue(name, out script);
        }
        private static KksAdvScript Copy(string bundle, string asset, ADV.ScenarioData source) => new KksAdvScript(bundle, asset,
            source.list.Select(row => new KksAdvScript.Row(row.Hash, row.Version, row.Multi, row.Command.ToString(), row.Args)));
    }
}
