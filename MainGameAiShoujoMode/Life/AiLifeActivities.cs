using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace MainGameAiShoujoMode.Life
{
#pragma warning disable CS0649
    [DataContract] internal sealed class AiLifeActivity
    {
        [DataMember] public string id, kind, sourceInventory, stage, point;
        [DataMember(EmitDefaultValue=false)] public string furnitureTarget;
        [DataMember(EmitDefaultValue = false)] public string animationActionId, animationLabel;
        [DataMember] public int category, item;
        [DataMember] public bool hasDestination;
        [DataMember] public float x, y, z;
        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(id) || (kind != "eat" && kind != "rest" && kind != "motion")
                || (stage != "queued" && stage != "travelling" && stage != "performing")
                || new[] { x, y, z }.Any(v => float.IsNaN(v) || float.IsInfinity(v))
                || (kind == "eat" && string.IsNullOrWhiteSpace(sourceInventory))
                || (furnitureTarget != null && (kind != "rest" || !furnitureTarget.StartsWith("bed:",StringComparison.Ordinal)))
                || (kind == "motion" && (string.IsNullOrWhiteSpace(animationActionId) || string.IsNullOrWhiteSpace(animationLabel)))) throw new SerializationException("生活行動の保存内容が不正です。");
        }
    }
    [DataContract] internal sealed class AiLifeActivityEffect
    {
        [DataMember] public string parameter;
        [DataMember] public float amount;
    }
    [DataContract] internal sealed class AiLifeActivityDefinition
    {
        [DataMember] public string key, label, placeholderState, materialStatus, effectsStatus;
        [DataMember(EmitDefaultValue = false)] public string animationActionId;
        [DataMember] public float seconds;
        [DataMember] public AiLifeActivityEffect[] effects;
    }
    [DataContract] internal sealed class AiLifeActivityCatalog
    {
        [DataMember] public AiLifeActivityDefinition[] definitions;
        [DataMember] public string databaseSha256, handBone;
        [DataMember] public float minimumTravel, maximumTravel, markerSize, markerHeight, foodSize;
        [DataMember] public int one, pageSize;
        internal AiLifeAnimationCatalog Animations;
        internal AiLifeActivityDefinition Find(string kind) => definitions.Single(v => v.key == kind);
        internal static AiLifeActivityCatalog Load(string content)
        {
            using (var stream = File.OpenRead(Path.Combine(content, "ai-life-activities.json")))
            {
                var value = (AiLifeActivityCatalog)new DataContractJsonSerializer(typeof(AiLifeActivityCatalog)).ReadObject(stream);
                if (value?.definitions == null || value.definitions.Length != new[] { "eat", "rest" }.Length
                    || !value.definitions.Select(v => v.key).OrderBy(v => v).SequenceEqual(new[] { "eat", "rest" })
                    || value.one <= default(int) || value.pageSize <= default(int) || string.IsNullOrWhiteSpace(value.handBone)
                    || string.IsNullOrWhiteSpace(value.databaseSha256) || value.maximumTravel < value.minimumTravel
                    || new[] { value.minimumTravel, value.maximumTravel, value.markerSize, value.markerHeight, value.foodSize }.Any(v => v <= default(float) || float.IsNaN(v) || float.IsInfinity(v))) throw new InvalidDataException("生活行動の接続定義が不正です。");
                foreach (var d in value.definitions)
                {
                    if (d.seconds <= default(float) || float.IsNaN(d.seconds) || float.IsInfinity(d.seconds) || d.placeholderState != "Idle" || d.effects == null) throw new InvalidDataException("生活行動の仮演技が不正です。");
                    foreach (var effect in d.effects)
                        if (effect == null || !new[] { "health", "fullness", "fatigue", "mood", "motivation", "erotic" }.Contains(effect.parameter) || float.IsNaN(effect.amount) || float.IsInfinity(effect.amount)) throw new InvalidDataException("生活行動の完了効果が不正です。");
                }
                value.Animations = AiLifeAnimationCatalog.Load(content);
                foreach (var d in value.definitions.Where(d => d.animationActionId != null))
                    if (value.Animations == null) throw new InvalidDataException("生活動作の再生用データがありません。"); else value.Animations.Sequence(d.animationActionId);
                return value;
            }
        }
    }
    internal sealed partial class AiLifeController
    {
        internal AiLifeActivityCatalog Activities => _runtime.Activities;
        internal void QueueSourceMotion(string action, string operation)
        {
            if (InH) throw new InvalidOperationException("H中は生活動作を開始できません。");
            var animations = Activities?.Animations ?? throw new InvalidOperationException("生活動作の接続データがありません。");
            var steps = animations.Sequence(action);
            var label = animations.Actions.SingleOrDefault(v => v.id == action)?.description ?? steps.Single(v => v.Phase == "main").Clip.sourceName;
            if (string.IsNullOrWhiteSpace(operation)) throw new ArgumentException("操作IDがありません。");
            var world = _runtime.Store.Read();
            var previous = world.operations.SingleOrDefault(v => v.request.id == "activity:" + operation + ":request");
            if (previous != null)
            {
                if (previous.request.resident?.id != ResidentId || previous.request.resident.activity?.animationActionId != action) throw new InvalidOperationException("操作IDと生活動作が一致しません。");
                return;
            }
            var state = world.residents.Single(v => v.id == ResidentId);
            if (state.activity != null) throw new InvalidOperationException("進行中の生活行動を中止してから頼んでください。");
            if (state.sickness != Rules.healthySickness) throw new InvalidOperationException("病気のため実行できません。");
            state.activity = new AiLifeActivity { id = operation, kind = "motion", animationActionId = action, animationLabel = label, stage = "queued" };
            state.action = "motion"; state.destination = null; state.following = null; state.blockedReason = null;
            state.lastActivityResult = label + "の動作を受け付けました。設備・小物・成果・効果は未接続です。";
            Commit(world, state, "activity:" + operation + ":request");
        }
        internal void QueueActivity(string kind, string operation, string inventory = null, int category = default, int item = default, string furnitureTarget = null)
        {
            if (InH) throw new InvalidOperationException("H中は生活行動を頼めません。");
            var definition = Activities?.Find(kind) ?? throw new InvalidOperationException("生活行動の接続データがありません。");
            var world = _runtime.Store.Read(); var state = world.residents.Single(v => v.id == ResidentId);
            if (string.IsNullOrWhiteSpace(operation)) throw new ArgumentException("操作IDがありません。");
            var previous = world.operations.SingleOrDefault(v => v.request.id == "activity:" + operation + ":request");
            if (previous != null)
            {
                var old = previous.request.resident;
                if (old?.id != ResidentId || old.activity?.kind != kind || old.activity?.furnitureTarget != furnitureTarget || (inventory != null && (old.activity.sourceInventory != inventory || old.activity.category != category || old.activity.item != item))) throw new InvalidOperationException("操作IDと生活行動が一致しません。");
                return;
            }
            if (state.activity != null) throw new InvalidOperationException("進行中の生活行動を中止してから頼んでください。");
            if (state.sickness != Rules.healthySickness) throw new InvalidOperationException("病気のため実行できません。先に状態を確認してください。");
            if (kind == "eat")
            {
                var candidate = world.inventories.Where(v => inventory == null ? v.Key == "agent:" + ResidentId || v.Key == _runtime.PlayerInventoryKey : v.Key == inventory)
                    .OrderBy(v => v.Key == "agent:" + ResidentId ? default(int) : Activities.one)
                    .SelectMany(v => v.items.Where(s => s.Count > default(int) && _runtime.Items.Find(s.CategoryID, s.ID)?.food == true
                        && (inventory == null || (s.CategoryID == category && s.ID == item))).Select(s => new { stock = s, owner = v.Key })).FirstOrDefault();
                if (candidate == null || (inventory != null && inventory != _runtime.PlayerInventoryKey && inventory != "agent:" + ResidentId)) throw new InvalidOperationException("選んだ食べ物がありません。女の子かプレイヤーの在庫を確認してください。");
                inventory = candidate.owner; category = candidate.stock.CategoryID; item = candidate.stock.ID;
            }
            if(furnitureTarget!=null&&(kind!="rest"||!furnitureTarget.StartsWith("bed:",StringComparison.Ordinal)))throw new InvalidOperationException("家具の休息対象が不正です。");
            state.activity = new AiLifeActivity { id = operation, kind = kind, sourceInventory = inventory, category = category, item = item, stage = "queued", furnitureTarget=furnitureTarget };
            state.action = kind; state.destination = null; state.following = null; state.blockedReason = null;
            state.lastActivityResult = definition.label + (furnitureTarget==null?"を受け付けました。仮の地点へ移動します。":"を受け付けました。指定されたベッドへ移動します。");
            Commit(world, state, "activity:" + operation + ":request");
        }
        internal void ActivityDestination(string id, string point, float x, float y, float z)
        {
            var world = _runtime.Store.Read(); var state = world.residents.Single(v => v.id == ResidentId);
            if (state.activity?.id != id) return;
            state.activity.hasDestination = true; state.activity.point = point; state.activity.x = x; state.activity.y = y; state.activity.z = z;
            state.activity.stage = "travelling"; state.destination = point;
            Commit(world, state, Guid.NewGuid().ToString("N"));
        }
        internal void ActivityArrived(string id)
        {
            var world = _runtime.Store.Read(); var state = world.residents.Single(v => v.id == ResidentId);
            if (state.activity?.id != id) return;
            if (!state.activity.hasDestination) throw new InvalidOperationException("生活行動の到着先が未確定です。");
            state.activity.stage = "performing"; state.lastActivityResult = state.activity.kind == "motion" ? state.activity.animationLabel + "の動作中。設備・小物・成果・効果は未接続です。" : Activities.Find(state.activity.kind).label + "中：" + Activities.Find(state.activity.kind).materialStatus;
            Commit(world, state, Guid.NewGuid().ToString("N"));
        }
        internal bool CompleteActivity(string id)
        {
            if (InH || Conversing) return false;
            var world = _runtime.Store.Read(); var state = world.residents.Single(v => v.id == ResidentId); var activity = state.activity;
            if (activity?.id != id || activity.stage != "performing" || state.sickness != Rules.healthySickness) return false;
            var d = activity.kind == "motion" ? null : Activities.Find(activity.kind);
            var request = new AiLifeStateStore.Request { id = "activity:" + id + ":complete", action = "state", resident = state };
            if (activity.kind == "eat")
            { request.action = "consume"; request.sources = new[] { activity.sourceInventory }; request.category = activity.category; request.item = activity.item; request.count = Activities.one; }
            foreach (var effect in d?.effects ?? Array.Empty<AiLifeActivityEffect>())
            {
                float Change(float current) => (float)Math.Max(Rules.minimum, Math.Min(Rules.maximum, (double)current + effect.amount));
                switch (effect.parameter)
                {
                    case "health": state.health = Change(state.health); break;
                    case "fullness": state.fullness = Change(state.fullness); break;
                    case "fatigue": state.fatigue = Change(state.fatigue); break;
                    case "mood": state.mood = Change(state.mood); break;
                    case "motivation": state.motivation = Change(state.motivation); break;
                    case "erotic": state.erotic = Change(state.erotic); break;
                }
            }
            state.lastActivityResult = activity.kind == "motion" ? activity.animationLabel + "の動作完了。成果・効果は反映していません。" : d.label + "完了。" + (activity.kind == "eat" ? "食べ物を消費しました。" : "") + d.effectsStatus;
            state.activity = null; state.action = "normal"; state.destination = null; state.blockedReason = null;
            _runtime.Store.Commit(world, request, out _, out var applied); Changed?.Invoke(State); return applied;
        }
        internal void CancelActivity(string reason, string expectedId = null)
        {
            var world = _runtime.Store.Read(); var state = world.residents.Single(v => v.id == ResidentId);
            if (state.activity == null || (expectedId != null && state.activity.id != expectedId)) return;
            state.activity = null; state.action = "normal"; state.destination = null; state.blockedReason = reason;
            state.lastActivityResult = reason;
            Commit(world, state, Guid.NewGuid().ToString("N"));
        }
    }
#pragma warning restore CS0649
}
