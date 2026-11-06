using System;
using MainGameAiShoujoMode.Life;

namespace MainGameAiShoujoMode.Conversation
{
    internal static class AiConversationActionExecution
    {
        internal static string Execute(AiReplyAction action, string operation, AiLifeController resident,
            AiLifeRuntime life, int quantity, Action beginH, Action endConversation)
        {
            if (!resident.Conversing || resident.InH) throw new InvalidOperationException("会話中の相手ではありません。");
            switch (action.Action)
            {
                case "talk": return "会話を続けます。";
                case "h":
                    if (action.Target != "player" || beginH == null || resident.State.sickness != resident.Rules.healthySickness)
                        throw new InvalidOperationException("今はHを開始できません。");
                    beginH(); return "Hへの移行を要求しました。";
                case "receive_item":
                    if (action.Inventory != life.PlayerInventoryKey || action.Target != "item:" + action.Inventory + ":" + action.Category + ":" + action.Item)
                        throw new InvalidOperationException("受け渡す品物がプレイヤーの所持品と一致しません。");
                    var result = resident.Connections.Execute(new AiLifeConnection { key = "item:transfer", kind = "transfer",
                        category = action.Category, item = action.Item, count = quantity }, operation);
                    if (!result.Completed) throw new InvalidOperationException(result.Message);
                    return result.Message;
                case "follow":
                    if (action.Target != "player" || resident.State.sickness != resident.Rules.healthySickness)
                        throw new InvalidOperationException("今は同行できません。");
                    resident.Execute("follow", operation); endConversation(); return "同行を指示しました。";
                case "normal":
                    resident.Execute("normal", operation); endConversation(); return "同行を終了し、通常の生活へ戻しました。";
                case "rest": case "eat":
                    resident.QueueActivity(action.Action, operation, action.Inventory, action.Category, action.Item,
                        action.Action == "rest" ? action.Target : null);
                    endConversation(); return "生活行動を受け付けました。完了はゲーム側で判断します。";
                default: throw new InvalidOperationException("未対応の行動です。");
            }
        }
    }
}
