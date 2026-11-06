using System;

namespace MainGameAiShoujoMode
{
    // The same public entry points used by Free H's PlaybackBar. No scene discovery.
    internal sealed class HItemCommands
    {
        internal sealed class Item
        {
            internal readonly string Label, Guid, Primary, Delete, Hint;
            internal Item(string label, string suffix, bool syringe = false)
            {
                Label = label; Guid = "com.kks.maingame." + suffix;
                Primary = syringe ? "PrimaryAction" : "PlaybackBarPrimaryAction";
                Delete = syringe ? "DeleteSyringe" : "PlaybackBarDelete";
                Hint = syringe ? "GetPrimaryActionHint" : "GetPlaybackBarActionHint";
            }
        }
        internal static readonly Item[] Items = {
            new Item("ディルド", "vrdildo"), new Item("鎖", "vrneckchain"),
            new Item("注射器", "syringe", true), new Item("iPhone", "iphonecamera"),
            new Item("マジック", "magicpaint"), new Item("ロウソク", "candle"),
            new Item("ディスプレイ", "desktopdisplay"), new Item("手錠", "vrhandcuffs")
        };
        private readonly Func<bool> _active;
        private readonly Func<string, bool> _allowed;
        private readonly Func<string, object> _resolve;
        internal HItemCommands(Func<bool> active, Func<string, bool> allowed, Func<string, object> resolve)
        { _active = active; _allowed = allowed; _resolve = resolve; }

        internal string Invoke(int index, bool delete)
        {
            if (!_active()) return "Hシーンは終了しています。";
            var item = Items[index];
            // Cleanup remains available after permission revocation; item APIs own cleanup.
            if (!delete && !_allowed(item.Guid)) return item.Label + "：このHでは使用が許可されていません。";
            try
            {
                var plugin = _resolve(item.Guid);
                if (plugin == null) return item.Label + "：プラグインが読み込まれていません。";
                var type = plugin.GetType();
                var method = type.GetMethod(delete ? item.Delete : item.Primary, Type.EmptyTypes);
                if (method == null) return item.Label + "：対応する操作APIがありません。";
                method.Invoke(plugin, null);
                if (delete) return item.Label + "を削除しました。";
                return item.Label + "：" + (type.GetMethod(item.Hint, Type.EmptyTypes)?.Invoke(plugin, null) as string
                    ?? "左クリック：呼び出し→ギズモ表示 / 右クリック：削除");
            }
            catch (Exception ex) { return item.Label + "：操作できません（" + ex.GetBaseException().Message + "）"; }
        }
    }
}
