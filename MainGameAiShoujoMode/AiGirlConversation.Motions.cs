using System;
using System.Collections.Generic;
using System.Linq;
using MainGameAiShoujoMode.Conversation;

namespace MainGameAiShoujoMode
{
    public sealed partial class AiGirlConversation
    {
        private void ShowSourceMotionGroups()
        {
            var catalog = Life.Activities.Animations;
            if (catalog == null) { Present("生活動作のデータがありません。"); ShowAdvice(); return; }
            var choices = new List<KksConversationView.Choice>();
            foreach (var group in catalog.Actions.GroupBy(v => v.posture).OrderBy(v => v.Key))
            {
                var category = group.Key;
                choices.Add(new KksConversationView.Choice(group.First().description.Trim() + " ほか（" + group.Count(v => v.status == "connected") + "/" + group.Count() + "件）", () => ShowSourceMotions(category)));
            }
            choices.Add(new KksConversationView.Choice("動作素材から選ぶ", ShowMotionClipGroups));
            choices.Add(new KksConversationView.Choice("戻る", ShowAdvice));
            Present("原本の生活動作を選べます。現在地で開始・本動作・終了を再生します。設備・小物・成果・効果は未接続です。分母は原本の全件数です。");
            _view.SetChoices(choices);
        }
        private void ShowSourceMotions(int category, int page = default)
        {
            var config = Life.Activities;
            var entries = config.Animations.Actions.Where(v => v.posture == category).ToArray();
            var choices = new List<KksConversationView.Choice>();
            foreach (var entry in entries.Skip(page * config.pageSize).Take(config.pageSize))
            {
                var selected = entry;
                choices.Add(new KksConversationView.Choice(entry.description.Trim() + (entry.status == "connected" ? "" : "（未接続）"), () => ShowMotionDetail(selected.id, () => ShowSourceMotions(category, page))));
            }
            if (page > default(int)) choices.Add(new KksConversationView.Choice("前の動作", () => ShowSourceMotions(category, page - config.one)));
            if ((page + config.one) * config.pageSize < entries.Length) choices.Add(new KksConversationView.Choice("次の動作", () => ShowSourceMotions(category, page + config.one)));
            choices.Add(new KksConversationView.Choice("分類へ戻る", ShowSourceMotionGroups));
            _view.SetChoices(choices);
        }
        private void ShowMotionClipGroups()
        {
            var choices = new List<KksConversationView.Choice>();
            foreach (var group in Life.Activities.Animations.clips.GroupBy(v => v.sourceBundle))
            {
                var source = group.Key;
                var segments = source.Replace('\\', '/').Split('/');
                var label = string.Join("/", segments.Skip(Math.Max(default(int), segments.Length - Life.Activities.one - Life.Activities.one)));
                choices.Add(new KksConversationView.Choice(label + "（" + group.Count(v => v.status == "converted") + "/" + group.Count() + "件）", () => ShowMotionClipPrefixes(source)));
            }
            choices.Add(new KksConversationView.Choice("戻る", ShowSourceMotionGroups));
            Present("単独の動作素材を選べます。生活行動に使われていない素材も含みます。未変換の素材は理由だけ確認できます。");
            _view.SetChoices(choices);
        }
        private string MotionPrefix(string name) => string.Join("_", name.Split('_').Take(Life.Activities.one + Life.Activities.one));
        private void ShowMotionClipPrefixes(string source)
        {
            var choices = new List<KksConversationView.Choice>();
            foreach (var group in Life.Activities.Animations.clips.Where(v => v.sourceBundle == source).GroupBy(v => MotionPrefix(v.sourceName)).OrderBy(v => v.Key, StringComparer.Ordinal))
            {
                var prefix = group.Key;
                choices.Add(new KksConversationView.Choice(prefix + "（" + group.Count(v => v.status == "converted") + "/" + group.Count() + "件）", () => ShowMotionClips(source, default, prefix)));
            }
            choices.Add(new KksConversationView.Choice("素材の分類へ戻る", ShowMotionClipGroups));
            _view.SetChoices(choices);
        }
        private void ShowMotionClips(string source, int page = default, string prefix = null)
        {
            var config = Life.Activities;
            var entries = config.Animations.clips.Where(v => v.sourceBundle == source && (prefix == null || MotionPrefix(v.sourceName) == prefix)).OrderBy(v => v.sourceName, StringComparer.Ordinal).ToArray();
            var choices = new List<KksConversationView.Choice>();
            foreach (var entry in entries.Skip(page * config.pageSize).Take(config.pageSize))
            {
                var selected = entry;
                choices.Add(new KksConversationView.Choice(entry.sourceName + (entry.status == "converted" ? "" : "（未変換）"), () => ShowMotionDetail("clip:" + selected.id, () => ShowMotionClips(source, page, prefix))));
            }
            if (page > default(int)) choices.Add(new KksConversationView.Choice("前の素材", () => ShowMotionClips(source, page - config.one, prefix)));
            if ((page + config.one) * config.pageSize < entries.Length) choices.Add(new KksConversationView.Choice("次の素材", () => ShowMotionClips(source, page + config.one, prefix)));
            choices.Add(new KksConversationView.Choice("動作名へ戻る", () => ShowMotionClipPrefixes(source)));
            _view.SetChoices(choices);
        }
        private void ShowMotionDetail(string action, Action back)
        {
            var catalog = Life.Activities.Animations;
            var definition = catalog.Actions.SingleOrDefault(v => v.id == action);
            string text;
            var ready = false;
            try
            {
                var steps = catalog.Sequence(action); ready = true;
                text = (definition?.description ?? steps.First().Clip.sourceName) + "\n" + string.Join(" → ", steps.Select(v => v.Clip.sourceName));
            }
            catch (Exception error)
            {
                var clip = catalog.clips.SingleOrDefault(v => "clip:" + v.id == action);
                var reason = clip?.status == "native-object-motion" ? "小物・設備・動物側の動作です。再生対象のオブジェクトが未接続です。" : clip?.status == "native-scene-motion" ? "原本マップ内の動作です。対応するマップ設備が未接続です。" : definition?.reason ?? clip?.error ?? error.GetBaseException().Message;
                text = "未接続：" + (definition?.description ?? clip?.sourceName ?? action) + "\n" + reason;
            }
            Present(text + "\n動作のみの再生です。設備・小物・成果・効果は未接続です。");
            var operation = Guid.NewGuid().ToString("N"); var owner = Resident;
            _view.SetChoices(new[] {
                new KksConversationView.Choice("この動作を再生する", () =>
                {
                    if (_disposed || !Active || !ReferenceEquals(owner, Resident) || !owner.Conversing) return;
                    try { owner.QueueSourceMotion(action, operation); EndCore(true); }
                    catch (Exception error) { Present(error.GetBaseException().Message); _view.SetChoices(new[] { new KksConversationView.Choice("戻る", back) }); }
                }, ready),
                new KksConversationView.Choice("戻る", back)
            });
        }
    }
}
