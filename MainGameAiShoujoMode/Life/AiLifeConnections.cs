using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace MainGameAiShoujoMode.Life
{
#pragma warning disable CS0649 // JSON-populated definitions.
    [DataContract] internal sealed class AiLifePickupSettings
    {
        [DataMember] public string key;
        [DataMember] public int category, item, count;
        [DataMember] public float forward, right, size, height, reach, groundProbe;
    }
    // Explicit requests only. Pending ports never consume stock or report a completed effect.
    [DataContract] internal sealed class AiLifeConnection
    {
        [DataMember] public string key, label, kind, parameter, resource, message;
        [DataMember] public float amount;
        [DataMember] public int category, item, count;
    }
    [DataContract] internal sealed class AiLifeConnectionCatalog
    {
        [DataMember] public AiLifeConnection[] entries;
        [DataMember] public int outfitPageSize;
        [DataMember] public AiLifePickupSettings pickup;
        internal static AiLifeConnectionCatalog Load(string content)
        {
            using (var stream = File.OpenRead(Path.Combine(content, "ai-life-connections.json")))
            {
                var catalog = (AiLifeConnectionCatalog)new DataContractJsonSerializer(typeof(AiLifeConnectionCatalog)).ReadObject(stream);
                if (catalog?.entries == null || catalog.outfitPageSize <= default(int) || catalog.entries.Any(entry => entry == null || string.IsNullOrWhiteSpace(entry.key)
                    || string.IsNullOrWhiteSpace(entry.label)) || catalog.entries.Select(entry => entry.key).Distinct(StringComparer.Ordinal).Count() != catalog.entries.Length)
                    throw new InvalidDataException("接続一覧が不正です。");
                foreach (var entry in catalog.entries) AiLifeConnections.Validate(entry);
                if (catalog.pickup == null || string.IsNullOrWhiteSpace(catalog.pickup.key) || catalog.pickup.count <= default(int)
                    || new[] { catalog.pickup.size, catalog.pickup.height, catalog.pickup.reach, catalog.pickup.groundProbe }.Any(value => value <= default(float) || float.IsNaN(value) || float.IsInfinity(value))
                    || new[] { catalog.pickup.forward, catalog.pickup.right }.Any(value => float.IsNaN(value) || float.IsInfinity(value))) throw new InvalidDataException("仮拾得物の設定が不正です。");
                return catalog;
            }
        }
    }
    internal sealed class AiLifeConnectionResult
    {
        internal bool Completed, Replayed;
        internal string Message;
    }
    internal sealed class AiLifeConnections : IDisposable
    {
        private readonly AiLifeRuntime _runtime;
        private readonly AiLifeController _life;
        private readonly Dictionary<string, Func<AiLifeConnection, string, AiLifeConnectionResult>> _handlers
            = new Dictionary<string, Func<AiLifeConnection, string, AiLifeConnectionResult>>(StringComparer.Ordinal);
        private bool _disposed;
        internal AiLifeConnections(AiLifeRuntime runtime, AiLifeController life) { _runtime = runtime; _life = life; }
        internal void Register(string key, Func<AiLifeConnection, string, AiLifeConnectionResult> handler)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AiLifeConnections));
            if (string.IsNullOrWhiteSpace(key) || handler == null || _handlers.ContainsKey(key)) throw new ArgumentException("接続先が不正または重複しています。");
            _handlers.Add(key, handler);
        }
        internal static void Validate(AiLifeConnection request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.key) || float.IsNaN(request.amount) || float.IsInfinity(request.amount))
                throw new ArgumentException("接続要求が不正です。");
            switch (request.kind)
            {
                case "parameter":
                    if (!new[] { "health", "fullness", "fatigue", "mood", "motivation", "erotic" }.Contains(request.parameter)) throw new ArgumentException("変更できないパラメータです。");
                    break;
                case "pickup": case "transfer":
                    if (request.count <= default(int)) throw new ArgumentException("個数が不正です。");
                    break;
                case "outfit":
                    if (string.IsNullOrWhiteSpace(request.resource)) throw new ArgumentException("コーデの参照がありません。");
                    break;
                case "action":
                    if (!new[] { "eat", "rest", "work", "treatment", "normal", "follow" }.Contains(request.resource)) throw new ArgumentException("行動の入口が不正です。");
                    break;
                case "placeholder": break;
                default: throw new ArgumentException("未定義の接続種別です。");
            }
        }
        internal static string Intent(AiLifeConnection value)
        {
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(AiLifeConnection)).WriteObject(stream, value);
                return Convert.ToBase64String(stream.ToArray());
            }
        }
        internal AiLifeConnectionResult Execute(AiLifeConnection request, string operation)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AiLifeConnections));
            if (_life.InH) throw new InvalidOperationException("H中は生活操作を開始できません。");
            request = AiLifeStateStore.Copy(request);
            Validate(request);
            if (string.IsNullOrWhiteSpace(operation)) throw new ArgumentException("操作IDがありません。");
            if (request.kind == "pickup") return _runtime.Pickup(request);
            // Clone caller-owned intent before handing it to an adapter or persisting it.
            var intent = Intent(request);
            var world = _runtime.Store.Read();
            var operationId = request.kind == "pickup" ? "pickup:" + _runtime.PlayerInventoryKey + ":" + request.key : operation;
            var previous = world.operations.SingleOrDefault(receipt => receipt.request.id == operationId);
            if (previous != null)
            {
                if (previous.request.connectionKey != request.key || previous.request.connectionIntent != intent
                    || (request.kind != "pickup" && previous.request.resident?.id != _life.ResidentId)) throw new InvalidOperationException("同じ操作IDに異なる要求が渡されました。");
                return new AiLifeConnectionResult { Completed = true, Replayed = true, Message = "この操作は確定済みです。重ねて反映しません。" };
            }
            if (_handlers.TryGetValue(request.key, out var handler)) return handler(request, operationId)
                ?? throw new InvalidOperationException("接続先から結果が返りませんでした。");
            if (request.kind == "placeholder") return new AiLifeConnectionResult { Message = request.message ?? "入口に到達しました。最終処理は未接続です。状態・在庫は変更していません。" };
            var state = AiLifeStateStore.Copy(world.residents.Single(value => value.id == _life.ResidentId));
            var commit = new AiLifeStateStore.Request { id = operationId, action = "state", resident = state,
                connectionKey = request.key, connectionIntent = intent };
            string result;
            switch (request.kind)
            {
                case "parameter":
                    ChangeParameter(state, request.parameter, request.amount); result = "パラメータを変更・保存しました：" + request.parameter; break;
                case "pickup": case "transfer":
                    if (_runtime.Items?.Find(request.category, request.item) == null) throw new InvalidOperationException("品物がカタログにありません。");
                    commit.action = request.kind == "pickup" ? "acquire" : "transfer";
                    commit.destination = request.kind == "pickup" ? _runtime.PlayerInventoryKey : "agent:" + _life.ResidentId;
                    if (request.kind == "transfer") commit.sources = new[] { _runtime.PlayerInventoryKey };
                    commit.category = request.category; commit.item = request.item; commit.count = request.count;
                    result = request.kind == "pickup" ? "品物を拾い、プレイヤーの在庫へ保存しました。" : "品物を女の子の在庫へ渡しました。追加効果は設定していません。"; break;
                case "outfit":
                    if (!state.outfits.Contains(request.resource, StringComparer.OrdinalIgnoreCase)) state.outfits.Add(request.resource);
                    result = "女の子の着替え一覧へ保存しました。着用・反応は別の接続です。"; break;
                case "action":
                    if (_life.Activities != null && (request.resource == "eat" || request.resource == "rest"))
                    {
                        _life.QueueActivity(request.resource, operationId);
                        return new AiLifeConnectionResult { Completed = true, Message = "生活行動を受け付けました。会話を閉じると仮の地点へ移動し、完了後に復帰します。" };
                    }
                    state.activity = null;
                    state.action = request.resource; state.following = request.resource == "follow" ? "player" : null;
                    state.destination = null; state.blockedReason = null;
                    result = "行動要求を保存しました：" + request.resource + "。設備・演技・完了効果が未接続なら理由付きで待機します。"; break;
                default: throw new NotSupportedException(request.kind);
            }
            _runtime.Store.Commit(world, commit, out _, out _); _life.Refresh();
            return new AiLifeConnectionResult { Completed = true, Message = result };
        }
        private void ChangeParameter(AiResidentState state, string name, float delta)
        {
            float Change(float current) => (float)Math.Max(_life.Rules.minimum, Math.Min(_life.Rules.maximum, (double)current + delta));
            switch (name)
            {
                case "health": state.health = Change(state.health); break;
                case "fullness": state.fullness = Change(state.fullness); break;
                case "fatigue": state.fatigue = Change(state.fatigue); break;
                case "mood": state.mood = Change(state.mood); break;
                case "motivation": state.motivation = Change(state.motivation); break;
                case "erotic": state.erotic = Change(state.erotic); break;
            }
        }
        public void Dispose() { _disposed = true; _handlers.Clear(); }
    }
#pragma warning restore CS0649
}
