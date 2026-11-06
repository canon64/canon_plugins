using System;
using System.Linq;

namespace MainGameAiShoujoMode.Life
{
    // The one owner of life decisions. Unity objects and dialogue only consume its committed state.
    internal sealed partial class AiLifeController
    {
        private readonly AiLifeRuntime _runtime;
        internal AiLifeRules Rules { get; }
        internal string ResidentId { get; }
        internal AiLifeConnections Connections { get; }
        internal bool Conversing { get; private set; }
        internal bool InH { get; private set; }
        internal event Action<AiResidentState> Changed;
        internal AiResidentState State => _runtime.Store.Read().residents.Single(value => value.id == ResidentId);
        internal AiLifeController(AiLifeRuntime runtime, string id, string origin)
        {
            _runtime = runtime; Rules = runtime.Rules ?? throw new InvalidOperationException("AI life rules are missing.");
            Rules.Validate();
            ResidentId = id; runtime.BindResident(id); runtime.Store.EnsureResident(Rules.Create(id, origin));
            Connections = new AiLifeConnections(runtime, this);
        }
        internal string DesiredAction
        {
            get
            {
                if (InH) return "h";
                if (Conversing) return "conversation";
                var state = State;
                if (state.sickness != Rules.healthySickness) return "treatment";
                if (state.activity != null) return state.activity.kind;
                if (state.health <= Rules.lowHealth || state.fatigue >= Rules.maximum - Rules.lowHealth) return "rest";
                if (state.fullness <= Rules.lowNeed) return "eat";
                return state.action;
            }
        }
        internal void BeginConversation() { if (InH || Conversing) throw new InvalidOperationException("Resident is already occupied."); Conversing = true; Changed?.Invoke(State); }
        internal void EndConversation(bool resume = true) { Conversing = false; if (resume) Changed?.Invoke(State); }
        internal void BeginH() { Conversing = false; InH = true; Changed?.Invoke(State); }
        internal void EndH() { InH = false; Changed?.Invoke(State); }
        internal void Refresh() => Changed?.Invoke(State);
        internal void SavePosition(float x, float y, float z, float yaw)
        {
            var world = _runtime.Store.Read(); var state = world.residents.Single(value => value.id == ResidentId);
            if (state.hasPosition && state.x == x && state.y == y && state.z == z && state.yaw == yaw) return;
            state.hasPosition = true; state.x = x; state.y = y; state.z = z; state.yaw = yaw;
            Commit(world, state, Guid.NewGuid().ToString("N"));
        }

        internal void Execute(string command, string operation = null)
        {
            var world = _runtime.Store.Read(); var state = world.residents.Single(value => value.id == ResidentId);
            switch (command)
            {
                case "follow": state.action = "follow"; state.following = "player"; break;
                case "normal": state.action = "normal"; state.following = null; state.destination = null; break;
                case "idle": state.action = "idle"; state.following = null; state.destination = null; break;
                case "rest": state.action = "rest"; state.following = null; break;
                case "test:healthy":
                    state.sickness = Rules.healthySickness; state.sicknessSeconds = default;
                    state.usedMedicine = false; state.treated = false; state.treatmentSeconds = default;
                    state.health = Rules.health; state.fullness = Rules.fullness; state.fatigue = default; state.action = "normal"; state.following = null; break;
                case "test:cold": state.sickness = Rules.coldSickness; state.sicknessSeconds = default; state.usedMedicine = false; state.treated = false; break;
                case "test:hungry": state.fullness = Rules.minimum; break;
                case "test:work": state.action = "work"; state.destination = null; state.following = null; break;
                default: throw new NotSupportedException("Life command is not connected: " + command);
            }
            state.activity = null;
            state.blockedReason = null;
            Commit(world, state, operation ?? Guid.NewGuid().ToString("N"));
        }
        internal void Advance(double seconds)
        {
            if (seconds < default(double) || double.IsNaN(seconds) || double.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            var world = _runtime.Store.Read(); var state = world.residents.Single(value => value.id == ResidentId);
            state.gameSeconds += seconds;
            if (state.sickness != Rules.healthySickness) state.sicknessSeconds += seconds;
            // No invented per-minute status deltas: native result-table effects enter via confirmed actions.
            Commit(world, state, Guid.NewGuid().ToString("N"));
        }
        internal void Give(AiLifeStateStore.Snapshot world, string operation, int category, int item, int quantity, bool medicine)
        {
            var state = AiLifeStateStore.Copy(world.residents.Single(value => value.id == ResidentId));
            var definition = _runtime.Items?.Find(category, item) ?? throw new InvalidOperationException("アイテムの定義がありません。");
            var request = new AiLifeStateStore.Request { id = operation, sources = new[] { _runtime.PlayerInventoryKey },
                action = "consume", category = category, item = item, count = quantity, resident = state };
            if (medicine)
            {
                if (category != Rules.coldMedicineCategory || item != Rules.coldMedicineItem || state.sickness != Rules.coldSickness)
                    throw new InvalidOperationException("この薬の処置は現在の病状に接続されていません。");
                if (state.treated) throw new InvalidOperationException("処置済みです。");
                // Current walking-state branch corresponds to StartSicknessEvent(0), not the collapsed branch.
                state.sickness = Rules.healthySickness; state.sicknessSeconds = default; state.treatmentSeconds = default;
                state.usedMedicine = false; state.treated = false; state.coldLocked = true; state.temperature = Rules.temperature;
            }
            else
            {
                if (!_runtime.Items.CanPresent(category, item)) throw new InvalidOperationException("このアイテムは贈与対象ではありません。");
                if (definition.food || definition.drink)
                { request.action = "transfer"; request.destination = "agent:" + ResidentId; }
                else if (definition.giftFlavor != null)
                {
                    var flavor = definition.giftFlavor;
                    if (flavor.flavor < default(int) || flavor.flavor >= state.flavor.Length) throw new InvalidOperationException("成長項目が未定義です。");
                    var delta = flavor.amount;
                    state.flavor[flavor.flavor] = checked((int)Math.Max(default(long), Math.Min(Rules.flavorMaximum, (long)state.flavor[flavor.flavor] + delta)));
                    state.totalFlavor = checked((int)Math.Max(default(long), (long)state.totalFlavor + delta));
                    state.flavorAdditionAmount = checked((int)Math.Max(default(long), (long)state.flavorAdditionAmount + delta));
                    request.growthDelta = delta; request.growthMaximum = Rules.flavorMaximum;
                }
            }
            _runtime.Store.Commit(world, request, out _, out _); Changed?.Invoke(State);
        }
        internal void Block(string reason)
        {
            var world = _runtime.Store.Read(); var state = world.residents.Single(value => value.id == ResidentId);
            if (state.blockedReason == reason) return;
            state.blockedReason = reason; Commit(world, state, Guid.NewGuid().ToString("N"));
        }
        private void Commit(AiLifeStateStore.Snapshot world, AiResidentState state, string operation)
        {
            _runtime.Store.Commit(world, new AiLifeStateStore.Request { id = operation, action = "state", resident = state }, out _, out _);
            Changed?.Invoke(State);
        }
        internal string Summary()
        {
            var state = State;
            return "体調 " + state.health + "／満腹 " + state.fullness + "／疲労 " + state.fatigue + "／機嫌 " + state.mood
                + "\n病気 " + (state.sickness == Rules.healthySickness ? "なし" : state.sickness == Rules.coldSickness ? "風邪" : state.sickness.ToString())
                + "／成長 " + state.phase + "／友好 " + state.friendship + "／行動 " + DesiredAction
                + "\n成長値（女子力・信頼・人間性・本能・変態・危機管理・闇・社交性）：" + string.Join(" / ", state.flavor)
                + (state.activity == null ? "" : "\n生活行動：" + state.activity.kind + "／" + state.activity.stage + "／地点 " + state.activity.point)
                + (state.lastActivityResult == null ? "" : "\n生活行動の結果：" + state.lastActivityResult)
                + (string.IsNullOrEmpty(state.blockedReason) ? "" : "\n待機理由：" + state.blockedReason);
        }
    }
}
