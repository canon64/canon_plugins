using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;

namespace MainGameAiShoujoMode.Life
{
    // AIProject.StuffItemExtensions semantics. The owner supplies the source profile's
    // ItemSlotMax; ItemStackUpperLimit is a different limit used by other entry paths.
    internal sealed class AiInventoryOperations
    {
        [DataContract] internal sealed class Item
        {
            [DataMember] public int CategoryID, ID, Count;
            internal DateTime LatestDateTime;
            // JSON's default DataContract DateTime encoding truncates fractional milliseconds.
            // Preserve the original acquisition time rather than silently changing item order.
            [DataMember(Name = "LatestDateTime")] private string SerializedLatestDateTime
            {
                get => LatestDateTime.ToString("O", CultureInfo.InvariantCulture);
                set => LatestDateTime = DateTime.ParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            }
            internal Item Copy() => new Item { CategoryID = CategoryID, ID = ID, Count = Count, LatestDateTime = LatestDateTime };
        }
        private readonly int _stackLimit;
        private readonly Func<DateTime> _now;
        internal AiInventoryOperations(int itemSlotMax, Func<DateTime> now)
        {
            if (itemSlotMax <= default(int)) throw new ArgumentOutOfRangeException(nameof(itemSlotMax));
            _stackLimit = itemSlotMax; _now = now ?? throw new ArgumentNullException(nameof(now));
        }
        private static IEnumerable<Item> Matching(IEnumerable<Item> inventory, Item item)
            => item == null ? Enumerable.Empty<Item>() : inventory.Where(value => value.CategoryID == item.CategoryID && value.ID == item.ID);

        // Like the source, true means that a new stack was created, not that all items fit.
        // Existing stacks can change even when this returns false or capacity is exhausted.
        internal bool AddItem(ICollection<Item> inventory, Item item, int count, int? capacity = null)
        {
            item.LatestDateTime = _now();
            foreach (var existing in Matching(inventory, item).OrderByDescending(value => value.Count).ToArray())
            {
                var space = _stackLimit - existing.Count;
                if (space == default(int)) continue;
                var remaining = count - space;
                if (remaining > default(int)) { existing.Count = _stackLimit; count = remaining; }
                else { existing.Count += count; count = default; }
                existing.LatestDateTime = item.LatestDateTime;
                if (count <= default(int)) break;
            }
            bool created = false;
            while (count > default(int) && (!capacity.HasValue || inventory.Count < capacity.Value))
            {
                created = true; var remaining = count - _stackLimit;
                if (remaining > default(int))
                { item.Count = _stackLimit; inventory.Add(item.Copy()); count = remaining; }
                else { item.Count = count; inventory.Add(item); count = default; break; }
            }
            return created;
        }
        // True means at least one stack was emptied. A partial-stack removal returns false.
        // Remaining count is authoritative; the source permits partial removal on shortage.
        internal bool RemoveItem(List<Item> inventory, Item item, ref int count)
        {
            bool emptied = false;
            foreach (var existing in Matching(inventory, item).OrderBy(value => value.Count).ToArray())
            {
                var remaining = count - existing.Count;
                if (remaining >= default(int)) { existing.Count = default; count = remaining; emptied = true; }
                else { existing.Count -= count; count = default; break; }
            }
            if (emptied) inventory.RemoveAll(value => value.Count <= default(int));
            return emptied;
        }
        internal int RemoveStorages(IReadOnlyCollection<List<Item>> inventories, Item item, int count)
        {
            foreach (var inventory in inventories)
            { RemoveItem(inventory, item, ref count); if (count <= default(int)) break; }
            return count;
        }
        // Preserve the source's 'possible' calculation, including its reservation of the
        // requested new stacks. It is not simply the total free space in this inventory.
        internal bool CanAddItem(IReadOnlyCollection<Item> inventory, int capacity, Item item, int count, out int possible)
        {
            var existingSpace = Matching(inventory, item).Sum(value => _stackLimit - value.Count);
            var remainder = Math.Max(default(int), count - existingSpace);
            var stacks = (int)Math.Ceiling((double)remainder / _stackLimit);
            var slotsLeft = capacity - inventory.Count - stacks;
            possible = existingSpace;
            if (slotsLeft > default(int)) possible += _stackLimit * slotsLeft;
            if (slotsLeft >= default(int) && remainder > default(int)) possible += _stackLimit - remainder;
            return slotsLeft >= default(int) || (possible > default(int) && possible >= count);
        }
    }
}
