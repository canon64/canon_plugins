using System;
using System.Collections.Generic;
using System.Linq;
using MainGameAiShoujoMode.Conversation;
using MainGameAiShoujoMode.Life;

namespace MainGameAiShoujoMode
{
    public sealed partial class AiGirlConversation
    {
        private void QueueLifeActivity(string kind, string inventory = null, int category = default, int item = default, string operation = null)
        {
            var owner = Resident;
            if (_disposed || !Active || owner == null || !owner.Conversing) return;
            try
            {
                owner.QueueActivity(kind, operation ?? Guid.NewGuid().ToString("N"), inventory, category, item);
                Action accepted = () => { if (Active && ReferenceEquals(owner, Resident)) EndCore(true); };
                if (!TryPlayBoundVoice(kind + ".accept", accepted)) accepted();
            }
            catch (Exception error)
            {
                Action failed = () => { if (Active && ReferenceEquals(owner, Resident)) { Present(error.GetBaseException().Message); ShowAdvice(); } };
                // A preparation shortage is shown directly; it is not voiced as the girl's refusal.
                if (kind == "eat" && owner.State.sickness != owner.Rules.healthySickness && TryPlayBoundVoice("eat.reject", failed)) return;
                failed();
            }
        }
        private void ShowActivityFoods(int page = default)
        {
            var owner = Resident; var config = Life.Activities;
            var world = Life.Store.Read();
            var foods = world.inventories.Where(v => v.Key == "agent:" + owner.ResidentId || v.Key == Life.PlayerInventoryKey)
                .SelectMany(v => v.items.Where(s => s.Count > default(int) && Life.Items.Find(s.CategoryID, s.ID)?.food == true)
                    .Select(s => new { inventory = v.Key, stock = s, definition = Life.Items.Find(s.CategoryID, s.ID), label = v.kind == "agent" ? "女の子" : "あなた" })).ToArray();
            var choices = new List<KksConversationView.Choice>();
            foreach (var food in foods.Skip(page * config.pageSize).Take(config.pageSize))
            {
                var operation = Guid.NewGuid().ToString("N");
                choices.Add(new KksConversationView.Choice(food.label + "の" + food.definition.name + " ×" + food.stock.Count,
                    () => { if (ReferenceEquals(owner, Resident)) QueueLifeActivity("eat", food.inventory, food.stock.CategoryID, food.stock.ID, operation); }));
            }
            if (page > default(int)) choices.Add(new KksConversationView.Choice("前の品物", () => ShowActivityFoods(page - config.one)));
            if ((page + config.one) * config.pageSize < foods.Length) choices.Add(new KksConversationView.Choice("次の品物", () => ShowActivityFoods(page + config.one)));
            choices.Add(new KksConversationView.Choice("取消・アドバイスへ戻る", ShowAdvice));
            Present(foods.Any() ? "食べる物を選んでください。完了した時だけ消費します。" + config.Find("eat").materialStatus : "食べ物がありません。贈り物か、状態画面の確認用追加で用意してください。");
            _view.SetChoices(choices);
        }
        private void CancelLifeActivity()
        {
            Resident.CancelActivity("生活行動を中止しました。品物と完了効果は反映していません。");
            Present(Resident.Summary()); ShowAdvice();
        }
        private void AddTestActivityFood()
        {
            try
            {
                var food = Life.Items.items.First(v => !v.system && v.food);
                Life.Store.Commit(Life.Store.Read(), new AiLifeStateStore.Request { id = Guid.NewGuid().ToString("N"), action = "acquire",
                    destination = Life.PlayerInventoryKey, category = food.category, item = food.id, count = Life.Activities.one }, out _, out _);
                Present("確認用の" + food.name + "を追加しました。アドバイスの食事から選べます。");
            }
            catch (Exception error) { Present(error.GetBaseException().Message); }
            _view.SetChoices(new[] { new KksConversationView.Choice("状態へ戻る", ShowStatus), new KksConversationView.Choice("食事をお願いする", () => ShowActivityFoods()) });
        }
    }
}
