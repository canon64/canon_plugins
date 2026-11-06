using System;
using System.Collections.Generic;
using System.Linq;
using MainGameAiShoujoMode.Conversation;

namespace MainGameAiShoujoMode
{
    public sealed partial class AiGirlConversation
    {
        internal void ChooseDedicatedHParticipants(string playerRole, KeyValuePair<string, string>[] residents, string selected, Action<string> start)
        {
            if (_disposed || !Active) throw new InvalidOperationException("参加者選択を表示する会話がありません。");
            void Draw(string selection)
            {
                var choices = new List<KksConversationView.Choice>();
                choices.Add(new KksConversationView.Choice("主人公の役割: " + playerRole, () => { }, false));
                choices.Add(new KksConversationView.Choice((selection == null ? "✓ " : "") + "追加女性なし", () => Draw(null)));
                choices.AddRange(residents.Select(pair => new KksConversationView.Choice((selection == pair.Key ? "✓ " : "") + "追加女性: " + pair.Value, () => Draw(pair.Key))));
                choices.Add(new KksConversationView.Choice("この参加者で開始", () => start(selection)));
                choices.Add(new KksConversationView.Choice("会話へ戻る", ShowMenu));
                _view.SetChoices(choices);
            }
            Draw(residents.Any(r => r.Key == selected) ? selected : null);
        }
    }
}
