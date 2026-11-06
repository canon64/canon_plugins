using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace MainGameAiShoujoMode.Life
{
#pragma warning disable CS0649 // Populated by DataContractJsonSerializer from the extracted catalog.
    [DataContract] internal sealed class AiItemCatalog
    {
        [DataContract] internal sealed class Flavor
        {
            [DataMember] public int flavor, amount;
        }
        [DataContract] internal sealed class Item
        {
            [DataMember] public bool system, food, drink;
            [DataMember] public int category, id, grade, icon, nameHash, rate;
            [DataMember] public string name, explanation;
            [DataMember] public Flavor giftFlavor;
        }
        [DataContract] internal sealed class Filter
        {
            [DataMember] public int category;
            [DataMember] public int[] ids;
        }
        [DataMember] public string sourceProfileVersion;
        [DataMember] public Item[] items;
        [DataMember] public Filter[] presentFilters;
        internal static AiItemCatalog Load(string path, string profile)
        {
            AiItemCatalog catalog;
            using (var stream = File.OpenRead(path)) catalog = (AiItemCatalog)new DataContractJsonSerializer(typeof(AiItemCatalog)).ReadObject(stream);
            if (catalog == null || catalog.sourceProfileVersion != profile || catalog.items == null || catalog.presentFilters == null
                || catalog.items.Any(value => value == null || string.IsNullOrEmpty(value.name))
                || catalog.presentFilters.Any(value => value == null || value.ids == null)
                || catalog.items.Select(value => new { value.system, value.category, value.id }).Distinct().Count() != catalog.items.Length
                || catalog.items.Where(value => !value.system).Select(value => value.nameHash).Distinct().Count() != catalog.items.Count(value => !value.system))
                throw new InvalidDataException("AI item catalog is incomplete or has duplicate identities.");
            return catalog;
        }
        internal Item Find(int category, int id) => items.SingleOrDefault(value => !value.system && value.category == category && value.id == id);
        internal bool CanPresent(int category, int id)
        {
            if (category <= default(int) || presentFilters.All(value => value.category == default(int))) return true;
            var filters = presentFilters.Where(value => value.category == category).ToArray();
            var ids = filters.SelectMany(value => value.ids).ToArray();
            return filters.Any() && (!ids.Any() || ids.Contains(id));
        }
    }
#pragma warning restore CS0649
}
