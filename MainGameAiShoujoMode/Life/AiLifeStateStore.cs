using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace MainGameAiShoujoMode.Life
{
    // Independent AI-world state. KKS favor/stage and the base game's save are not its owners.
    // Residents, inventory and individual/world gift growth are published in one transaction.
    internal sealed class AiLifeStateStore
    {
        [DataContract] internal sealed class Inventory
        {
            [DataMember] public string kind, owner;
            [DataMember] public int? capacity;
            [DataMember] public List<AiInventoryOperations.Item> items = new List<AiInventoryOperations.Item>();
            internal string Key => kind + ":" + owner;
        }
        [DataContract] internal sealed class Request
        {
            [DataMember] public string id { get; set; }
            [DataMember] public string action { get; set; }
            [DataMember(EmitDefaultValue = false)] public string connectionKey { get; set; }
            [DataMember(EmitDefaultValue = false)] public string connectionIntent { get; set; }
            [DataMember] public string destination { get; set; }
            [DataMember] public string[] sources = Array.Empty<string>();
            [DataMember] public int category { get; set; }
            [DataMember] public int item { get; set; }
            [DataMember] public int count { get; set; }
            [DataMember] public bool allowPartial { get; set; }
            [DataMember(EmitDefaultValue = false)] public AiResidentState resident { get; set; }
            [DataMember(EmitDefaultValue = false)] public int growthDelta { get; set; }
            [DataMember(EmitDefaultValue = false)] public int growthMaximum { get; set; }
            [DataMember(EmitDefaultValue = false)] public int merchantReturnCategory, merchantReturnItem, merchantReturnCount;
            [DataMember(EmitDefaultValue = false)] public int nativeMerchantSpendDelta, nativeMerchantSpendMaximum;
            [DataMember(EmitDefaultValue = false)] public int merchantReturnRate;
        }
        [DataContract] internal sealed class NativeVendorItem
        {
            [DataMember] public int category, item, rate, stock, count;
            [DataMember] public int group = default(int);
        }
        [DataContract] internal sealed class Receipt
        {
            [DataMember] public Request request;
            [DataMember] public int applied, remaining;
        }
        [DataContract] internal sealed class Snapshot
        {
            [DataMember] public int schema;
            [DataMember] public string revision;
            [DataMember] public List<Inventory> inventories = new List<Inventory>();
            [DataMember] public List<Receipt> operations = new List<Receipt>();
            [DataMember] public List<AiResidentState> residents = new List<AiResidentState>();
            [DataMember] public int totalAgentFlavorAdditionAmount;
            [DataMember(EmitDefaultValue = false)] public int nativeMerchantSpendTotal;
            [DataMember(EmitDefaultValue = false)] public string nativeMerchantStockCycle;
            [DataMember(EmitDefaultValue = false)] public int nativeMerchantArea;
            [DataMember(EmitDefaultValue = false)] public List<NativeVendorItem> nativeMerchantVendor;
            [OnDeserialized] private void Loaded(StreamingContext context)
            { if (residents == null) residents = new List<AiResidentState>(); }
        }
        private readonly string _path;
        private readonly int _schema, _stackLimit;
        private readonly AiInventoryOperations _items;
        private static readonly string[] OwnerKinds = { "player", "agent", "storage", "pantry", "merchant" };
        internal AiLifeStateStore(string path, int schema, int itemSlotMax, Func<DateTime> now)
        {
            _path = Path.GetFullPath(path); _schema = schema; _stackLimit = itemSlotMax;
            _items = new AiInventoryOperations(itemSlotMax, now);
            using (Acquire()) Load();
        }
        private static byte[] Encode<T>(T value)
        { using (var stream = new MemoryStream()) { new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value); return stream.ToArray(); } }
        private static T Decode<T>(byte[] bytes)
        { using (var stream = new MemoryStream(bytes)) return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream); }
        internal static T Copy<T>(T value) => Decode<T>(Encode(value));
        private IDisposable Acquire()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            return new FileStream(_path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        private Snapshot Load()
        {
            var value = File.Exists(_path) ? Decode<Snapshot>(File.ReadAllBytes(_path)) : new Snapshot { schema = _schema, revision = "initial" };
            Validate(value); return value;
        }
        private static void ValidateRequest(Request request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.id) || request.sources == null || request.count < default(int)
                || request.sources.Any(string.IsNullOrWhiteSpace) || request.sources.Distinct(StringComparer.Ordinal).Count() != request.sources.Length)
                throw new InvalidDataException("AI inventory request is incomplete or has duplicate sources.");
            request.resident?.Validate();
            if (request.growthDelta != default(int) && (request.resident == null || request.growthMaximum <= default(int))) throw new InvalidDataException("Growth update requires resident and native limit.");
            if (request.action != "state" && request.count == default(int)) throw new InvalidDataException("Item quantity must be positive.");
            if (request.action != "merchant-trade" && (request.nativeMerchantSpendDelta != default(int) || request.nativeMerchantSpendMaximum != default(int)
                || request.merchantReturnCount != default(int) || request.merchantReturnCategory != default(int) || request.merchantReturnItem != default(int) || request.merchantReturnRate != default(int))) throw new InvalidDataException("Merchant mutation requires a complete atomic trade.");
            switch (request.action)
            {
                case "state":
                    if (request.resident == null || request.sources.Any() || request.destination != null || request.count != default(int)) throw new InvalidDataException("State transition must contain a resident and no item movement.");
                    break;
                case "acquire":
                    if (request.sources.Any() || string.IsNullOrWhiteSpace(request.destination)) throw new InvalidDataException("Acquisition requires only a destination.");
                    break;
                case "transfer":
                    if (!request.sources.Any() || string.IsNullOrWhiteSpace(request.destination) || request.sources.Contains(request.destination)) throw new InvalidDataException("Transfer needs distinct source and destination inventories.");
                    break;
                case "merchant-trade":
                    if (request.sources.Length != Math.Sign(request.count) || string.IsNullOrWhiteSpace(request.destination) || request.sources.Contains(request.destination)
                        || request.allowPartial || request.merchantReturnCount <= default(int) || request.nativeMerchantSpendDelta <= default(int)
                        || request.nativeMerchantSpendMaximum <= default(int) || request.resident != null) throw new InvalidDataException("Merchant trade requires two owned inventories and a complete verified exchange.");
                    break;
                case "consume":
                    if (!request.sources.Any() || request.destination != null) throw new InvalidDataException("Consumption requires sources and no destination.");
                    break;
                default: throw new InvalidDataException("Unsupported AI inventory action: " + request.action);
            }
        }
        private void Validate(Snapshot value)
        {
            if (value == null || value.schema != _schema || string.IsNullOrWhiteSpace(value.revision) || value.inventories == null || value.operations == null || value.nativeMerchantSpendTotal < default(int))
                throw new InvalidDataException("AI world inventory save is incomplete or has an unsupported schema.");
            if (value.nativeMerchantVendor != null && (string.IsNullOrWhiteSpace(value.nativeMerchantStockCycle)
                || value.nativeMerchantVendor.Any(v => v == null || v.rate <= default(int) || v.count < default(int) || v.stock < v.count)
                || value.nativeMerchantVendor.Select(v => new { v.category, v.item }).Distinct().Count() != value.nativeMerchantVendor.Count))
                throw new InvalidDataException("Native vendor save is invalid.");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var residents = new HashSet<string>(StringComparer.Ordinal);
            if (value.residents == null) throw new InvalidDataException("Missing AI residents.");
            foreach (var resident in value.residents)
            {
                if (resident == null || !residents.Add(resident.id)) throw new InvalidDataException("Duplicate AI resident.");
                resident.Validate();
            }
            foreach (var inventory in value.inventories)
            {
                if (inventory == null || !OwnerKinds.Contains(inventory.kind) || string.IsNullOrWhiteSpace(inventory.owner) || inventory.items == null
                    || inventory.capacity < default(int) || !keys.Add(inventory.Key)) throw new InvalidDataException("Invalid or duplicate AI inventory owner.");
                if (inventory.items.Any(item => item == null || item.Count <= default(int) || item.Count > _stackLimit)) throw new InvalidDataException("Invalid AI item stack.");
            }
            var operations = new HashSet<string>(StringComparer.Ordinal);
            foreach (var receipt in value.operations)
            {
                if (receipt == null) throw new InvalidDataException("Missing inventory receipt.");
                ValidateRequest(receipt.request);
                if (!operations.Add(receipt.request.id) || receipt.applied < default(int) || receipt.remaining < default(int)
                    || (long)receipt.applied + receipt.remaining != receipt.request.count
                    || (!receipt.request.allowPartial && receipt.remaining != default(int))) throw new InvalidDataException("Invalid inventory operation receipt.");
                if (receipt.request.sources.Any(key => !keys.Contains(key)) || (receipt.request.destination != null && !keys.Contains(receipt.request.destination)))
                    throw new InvalidDataException("Inventory receipt references a missing owner.");
            }
        }
        internal Snapshot Read() { using (Acquire()) return Copy(Load()); }
        internal Snapshot EnsureNativeVendor(string merchantKey, string cycle, NativeVendorItem[] vendor, int area = default(int))
        {
            if (string.IsNullOrWhiteSpace(cycle) || vendor == null || vendor.Any(v => v == null || v.rate <= default(int) || v.count < default(int) || v.stock < v.count)
                || vendor.Select(v => new { v.category, v.item }).Distinct().Count() != vendor.Length) throw new InvalidDataException("Native vendor stock is incomplete.");
            vendor = Copy(vendor);
            using (Acquire())
            {
                var current = Load();
                if (current.nativeMerchantStockCycle == cycle) return Copy(current);
                var merchant = current.inventories.Single(v => v.Key == merchantKey && v.kind == "merchant");
                // The vendor's unsold stock is replaced, while items previously received from the player remain owned.
                foreach (var old in current.nativeMerchantVendor ?? new List<NativeVendorItem>())
                {
                    int remaining = old.count;
                    _items.RemoveItem(merchant.items, new AiInventoryOperations.Item { CategoryID = old.category, ID = old.item, Count = old.count }, ref remaining);
                    if (remaining != default(int)) throw new InvalidDataException("Saved vendor stock differs from the owned inventory.");
                }
                foreach (var row in vendor.Where(v => v.count > default(int)))
                    _items.AddItem(merchant.items, new AiInventoryOperations.Item { CategoryID = row.category, ID = row.item, Count = row.count }, row.count, merchant.capacity);
                current.nativeMerchantVendor = vendor.ToList(); current.nativeMerchantStockCycle = cycle; current.nativeMerchantArea = area;
                current.revision = Guid.NewGuid().ToString("N"); Publish(current); return Copy(current);
            }
        }
        internal Snapshot EnsureResident(AiResidentState resident)
        {
            resident = Copy(resident); resident.Validate();
            using (Acquire())
            {
                var current = Load(); var existing = current.residents.SingleOrDefault(value => value.id == resident.id);
                if (existing != null)
                {
                    if (existing.origin != resident.origin) throw new InvalidDataException("Resident identity belongs to another origin.");
                    return Copy(current);
                }
                current.residents.Add(resident); current.revision = Guid.NewGuid().ToString("N"); Publish(current); return Copy(current);
            }
        }
        internal Snapshot EnsureInventory(string kind, string owner, int? capacity)
        {
            if (!OwnerKinds.Contains(kind) || string.IsNullOrWhiteSpace(owner) || capacity < default(int)) throw new ArgumentException("Invalid AI inventory owner/capacity.");
            using (Acquire())
            {
                var current = Load(); var existing = current.inventories.SingleOrDefault(value => value.kind == kind && value.owner == owner);
                if (existing != null)
                { if (existing.capacity != capacity) throw new InvalidOperationException("Inventory capacity changed; use an explicit capacity transition."); return Copy(current); }
                current.inventories.Add(new Inventory { kind = kind, owner = owner, capacity = capacity });
                current.revision = Guid.NewGuid().ToString("N"); Publish(current); return Copy(current);
            }
        }
        private static long Count(Inventory inventory, Request request) => inventory.items.Where(item => item.CategoryID == request.category && item.ID == request.item).Sum(item => (long)item.Count);
        internal Snapshot Commit(Snapshot expected, Request request, out Receipt receipt, out bool applied)
        {
            if (expected == null) throw new ArgumentNullException(nameof(expected));
            // Detach caller-owned data before taking the lock or recording the immutable intent.
            request = Copy(request); ValidateRequest(request); applied = false;
            using (Acquire())
            {
                var current = Load(); var previous = current.operations.SingleOrDefault(value => value.request.id == request.id);
                if (previous != null)
                {
                    if (!Encode(previous.request).SequenceEqual(Encode(request))) throw new InvalidOperationException("Inventory operation ID was reused for a different intent.");
                    receipt = Copy(previous); return Copy(current);
                }
                if (expected.schema != _schema || expected.revision != current.revision) throw new InvalidOperationException("AI inventory state changed before confirmation.");
                if (request.resident != null)
                {
                    var index = current.residents.FindIndex(value => value.id == request.resident.id && value.origin == request.resident.origin);
                    if (index < default(int)) throw new InvalidOperationException("AI resident is not registered.");
                    current.residents[index] = Copy(request.resident);
                    if (request.growthDelta != default(int)) current.totalAgentFlavorAdditionAmount = checked((int)Math.Max(default(long), Math.Min(request.growthMaximum, (long)current.totalAgentFlavorAdditionAmount + request.growthDelta)));
                }
                Inventory Find(string key) => current.inventories.SingleOrDefault(value => value.Key == key) ?? throw new InvalidOperationException("AI inventory owner is missing: " + key);
                var sources = request.sources.Select(Find).ToArray(); var destination = request.destination == null ? null : Find(request.destination);
                if (request.action == "merchant-trade")
                {
                    if (sources.Single().kind != "player" || destination.kind != "merchant") throw new InvalidOperationException("Merchant exchange inventory ownership differs.");
                    var quote = current.nativeMerchantVendor?.SingleOrDefault(v => v.category == request.merchantReturnCategory && v.item == request.merchantReturnItem);
                    if (quote == null || quote.count < request.merchantReturnCount || quote.rate != request.merchantReturnRate)
                        throw new InvalidOperationException("Native vendor quote/remaining stock changed.");
                    if (request.nativeMerchantSpendDelta < checked(quote.rate * request.merchantReturnCount))
                        throw new InvalidOperationException("Native vendor value exceeds the player's offer.");
                    long stock = destination.items.Where(i => i.CategoryID == request.merchantReturnCategory && i.ID == request.merchantReturnItem).Sum(i => (long)i.Count);
                    if (stock < request.merchantReturnCount) throw new InvalidOperationException("Merchant has insufficient stock before exchange.");
                }
                var quantity = request.action == "acquire" ? request.count : (int)Math.Min(request.count, sources.Sum(value => Count(value, request)));
                if (!request.allowPartial && quantity != request.count) throw new InvalidOperationException("AI inventory has insufficient items.");
                var item = new AiInventoryOperations.Item { CategoryID = request.category, ID = request.item, Count = quantity };
                if (destination != null && quantity > default(int))
                {
                    var before = Count(destination, request);
                    _items.AddItem(destination.items, item, quantity, destination.capacity);
                    quantity = checked((int)(Count(destination, request) - before));
                    if (!request.allowPartial && quantity != request.count) throw new InvalidOperationException("AI destination inventory is full.");
                }
                if (sources.Any() && quantity > default(int))
                {
                    var remaining = _items.RemoveStorages(sources.Select(value => value.items).ToArray(), item, quantity);
                    if (remaining != default(int)) throw new InvalidOperationException("Inventory source quantity changed within the transaction.");
                }
                if (request.action == "merchant-trade")
                {
                    var returned = new AiInventoryOperations.Item { CategoryID = request.merchantReturnCategory, ID = request.merchantReturnItem, Count = request.merchantReturnCount };
                    int before = checked((int)sources.Single().items.Where(i => i.CategoryID == returned.CategoryID && i.ID == returned.ID).Sum(i => (long)i.Count));
                    _items.AddItem(sources.Single().items, returned, request.merchantReturnCount, sources.Single().capacity);
                    long after = sources.Single().items.Where(i => i.CategoryID == returned.CategoryID && i.ID == returned.ID).Sum(i => (long)i.Count);
                    if (after - before != request.merchantReturnCount) throw new InvalidOperationException("Player inventory cannot hold the complete merchant return.");
                    int remaining = request.merchantReturnCount;
                    _items.RemoveItem(destination.items, returned, ref remaining);
                    if (remaining != default(int)) throw new InvalidOperationException("Merchant stock changed within the transaction.");
                    current.nativeMerchantVendor.Single(v => v.category == returned.CategoryID && v.item == returned.ID).count -= request.merchantReturnCount;
                    current.nativeMerchantSpendTotal = checked((int)Math.Min(request.nativeMerchantSpendMaximum, (long)current.nativeMerchantSpendTotal + request.nativeMerchantSpendDelta));
                }
                receipt = new Receipt { request = request, applied = quantity, remaining = request.count - quantity };
                current.operations.Add(receipt); current.revision = Guid.NewGuid().ToString("N"); Publish(current);
                applied = true; receipt = Copy(receipt); return Copy(current);
            }
        }
        private void Publish(Snapshot value)
        {
            Validate(value);
            var pending = _path + ".pending-" + Guid.NewGuid().ToString("N");
            try
            {
                var bytes = Encode(value);
                using (var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, default, bytes.Length); stream.Flush(true); }
                if (File.Exists(_path)) File.Replace(pending, _path, _path + ".previous"); else File.Move(pending, _path);
            }
            finally { if (File.Exists(pending)) File.Delete(pending); }
        }
    }
}
