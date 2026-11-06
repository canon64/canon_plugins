using System;
using System.Linq;
using System.IO;

namespace MainGameAiShoujoMode.Life
{
    // Explicit mode entry and stable resident binding only; no frame monitoring.
    internal sealed class AiLifeRuntime
    {
        private readonly AiLifeRuntimeSettings _settings;
        private readonly Action<string> _log;
        internal AiLifeStateStore Store { get; }
        internal AiLifeRules Rules => _settings.rules;
        internal string PlayerInventoryKey => "player:" + _settings.worldKey;
        internal string MerchantInventoryKey => "merchant:" + _settings.worldKey;
        internal void EnsureNativeMerchant() => Ensure("merchant", _settings.worldKey, null);
        private string _itemContentDirectory;
        private NativeMerchantCatalog _merchantCatalog;
        internal AiLifeStateStore.Snapshot OpenNativeMerchant(int area, string gameDay, Func<int,int,int> random)
        {
            if (string.IsNullOrWhiteSpace(gameDay) || random == null || _itemContentDirectory == null) throw new InvalidOperationException("Native merchant world/day/items are unavailable.");
            if (_merchantCatalog == null) _merchantCatalog = NativeMerchantCatalog.Load(Path.Combine(_itemContentDirectory, "native-merchant-catalog.json"));
            string cycle = "native-vendor:" + _merchantCatalog.itemCatalogSha256 + ":" + area + ":" + gameDay;
            var current = Store.Read();
            if (current.nativeMerchantStockCycle == cycle) return current;
            EnsureNativeMerchant();
            return Store.EnsureNativeVendor(MerchantInventoryKey, cycle, _merchantCatalog.NormalStock(area, random), area);
        }
        internal bool TradeNativeMerchant(string operationId, int giveCategory, int giveItem, int giveCount, int receiveCategory, int receiveItem, int receiveCount, int nativeMaximum)
        {
            if (string.IsNullOrWhiteSpace(operationId) || giveCount <= default(int) || receiveCount <= default(int)) throw new ArgumentException("取引IDと個数が必要です。");
            var giving = Items.Find(giveCategory, giveItem) ?? throw new ArgumentException("渡す原本itemがありません。");
            if (Items.Find(receiveCategory, receiveItem) == null) throw new ArgumentException("受け取る原本itemがありません。");
            var state = Store.Read();
            var quote = state.nativeMerchantVendor?.SingleOrDefault(v => v.category == receiveCategory && v.item == receiveItem) ?? throw new InvalidOperationException("商人の実在庫を開いてください。");
            if (giving.rate <= default(int) || quote.rate <= default(int)) throw new InvalidOperationException("原本で取引価値が無効なitemです。");
            int x = checked(giving.rate * giveCount), y = checked(quote.rate * receiveCount);
            if (x < y) throw new InvalidOperationException("原本の取引条件x>=yを満たしていません。");
            EnsureNativeMerchant();
            Store.Commit(state, new AiLifeStateStore.Request { id = operationId, action = "merchant-trade", sources = new[] { PlayerInventoryKey }, destination = MerchantInventoryKey,
                category = giveCategory, item = giveItem, count = giveCount, merchantReturnCategory = receiveCategory, merchantReturnItem = receiveItem, merchantReturnCount = receiveCount,
                merchantReturnRate = quote.rate, nativeMerchantSpendDelta = x, nativeMerchantSpendMaximum = nativeMaximum, connectionKey = "native-merchant", connectionIntent = "native-catalog-and-vendor-rate" }, out _, out bool applied);
            return applied;
        }
        internal AiItemCatalog Items { get; private set; }
        internal AiLifeConnectionCatalog ConnectionCatalog { get; private set; }
        internal AiLifeActivityCatalog Activities { get; private set; }
        internal void LoadActivities(string contentDirectory) => Activities = AiLifeActivityCatalog.Load(contentDirectory);
        internal void LoadConnections(string contentDirectory) => ConnectionCatalog = AiLifeConnectionCatalog.Load(contentDirectory);
        internal AiLifeConnectionResult Pickup(AiLifeConnection entry)
        {
            entry = AiLifeStateStore.Copy(entry); AiLifeConnections.Validate(entry);
            if (entry.kind != "pickup" || Items?.Find(entry.category, entry.item) == null) throw new InvalidOperationException("拾得品の定義がありません。");
            var request = new AiLifeStateStore.Request { id = "pickup:" + PlayerInventoryKey + ":" + entry.key,
                action = "acquire", destination = PlayerInventoryKey, category = entry.category, item = entry.item, count = entry.count,
                connectionKey = entry.key, connectionIntent = AiLifeConnections.Intent(entry) };
            Store.Commit(Store.Read(), request, out _, out var applied);
            return new AiLifeConnectionResult { Completed = true, Replayed = !applied,
                Message = applied ? "品物を拾い、プレイヤーの在庫へ保存しました。" : "この拾得物は取得済みです。重ねて追加しません。" };
        }
        internal void LoadItems(string contentDirectory)
        {
            if (string.IsNullOrWhiteSpace(_settings.itemCatalogFile) || Path.GetFileName(_settings.itemCatalogFile) != _settings.itemCatalogFile)
                throw new InvalidDataException("AI item catalog must be a content filename.");
            Items = AiItemCatalog.Load(Path.Combine(contentDirectory, _settings.itemCatalogFile), _settings.sourceProfileVersion);
            _itemContentDirectory = contentDirectory;
        }
        internal AiLifeRuntime(AiLifeRuntimeSettings settings, Func<DateTime> now, Action<string> log)
        {
            settings.Validate(); _settings = settings; _log = log;
            Store = new AiLifeStateStore(settings.savePath, settings.schema, settings.itemSlotMax, now);
            if (Store.Read().inventories.Any(value => value.kind != "agent" && value.owner != settings.worldKey))
                throw new InvalidDataException("AI life save belongs to another world; use its own save path.");
            Ensure("player", settings.worldKey, settings.playerInitialCapacity);
            Ensure("storage", settings.worldKey, settings.storageCapacity);
            Ensure("pantry", settings.worldKey, settings.pantryCapacity);
            _log?.Invoke("AI_LIFE_READY world=" + settings.worldKey + " inventories=" + Store.Read().inventories.Count + " sourceProfile=" + settings.sourceProfileVersion);
        }
        private void Ensure(string kind, string owner, int? initialCapacity)
        {
            var existing = Store.Read().inventories.SingleOrDefault(value => value.kind == kind && value.owner == owner);
            // Persisted capacity is world state; startup must not reset an acquired upgrade.
            Store.EnsureInventory(kind, owner, existing == null ? initialCapacity : existing.capacity);
        }
        internal string BindResident(string stableIdentity)
        {
            if (!Guid.TryParseExact(stableIdentity, "N", out _)) throw new ArgumentException("AI resident inventory requires its saved stable UUID.", nameof(stableIdentity));
            Ensure("agent", stableIdentity, _settings.agentInventoryCapacity);
            _log?.Invoke("AI_LIFE_RESIDENT_BOUND resident=" + stableIdentity);
            return "agent:" + stableIdentity;
        }
    }
}
