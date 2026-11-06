using System;
using System.IO;
using System.Linq;

namespace MainGameAiShoujoH.Presentation
{
    // Pure load selection: no filesystem access or Unity asset loading until Load is called under the transition.
    internal sealed class NativeItemLoadPlan
    {
        internal ItemDefinition[] Items { get; }
        private readonly ItemFile[] files;
        private NativeItemLoadPlan(ItemDefinition[] items, ItemFile[] files) { Items = items; this.files = files; }
        internal static NativeItemLoadPlan Create(ItemCatalog catalog, int mode, int motionId)
        {
            if (catalog?.items == null || catalog.files == null) throw new InvalidDataException("Native H-item catalog is missing.");
            var selected = catalog.items.Where(d => d.mode == mode && d.motionId == motionId).ToArray();
            if (selected.Length == default(int)) return new NativeItemLoadPlan(selected, Array.Empty<ItemFile>());
            if (!catalog.runtimeVerified) throw new InvalidOperationException("Native H-item rig/binding verification is unavailable for the selected pose.");
            var required = selected.SelectMany(d => string.IsNullOrEmpty(d.controller) ? new[] { d.bundle } : new[] { d.bundle, d.baseBundle, d.controllerBundle }).Distinct(StringComparer.Ordinal).ToArray();
            if (required.Any(string.IsNullOrEmpty)) throw new InvalidDataException("Selected native H-item bundle definition is incomplete.");
            return new NativeItemLoadPlan(selected, required.Select(name => catalog.files.Single(f => f.source == name)).ToArray());
        }
        internal void Load(Action<ItemFile> load)
        {
            if (load == null) throw new ArgumentNullException(nameof(load));
            foreach (var file in files) load(file);
        }
    }
}
