using System;
using System.Collections.Generic;
using System.Linq;
using MainGameAiShoujoMode.Conversation;

namespace MainGameAiShoujoMode
{
    public sealed partial class AiGirlConversation
    {
        private KksCharacterVoiceCatalog _characterVoices;
        private bool _endingVoice;
        private bool TryPlayBoundVoice(string key, Action completed)
        {
            if (_disposed || !Active || _native == null) return false;
            var binding = _characterVoices.Slot(_native.Heroine.voiceNo, _native.Stage, key);
            if (binding == null) return false;
            var entry = _characterVoices.Find(binding.entryId); var session = _native;
            try
            {
                _placeholderVoice?.Dispose(); _placeholderVoice = null; EnsurePresentation();
                // A voice part is detached from its original result; only the caller changes AI state.
                var row = new KksConversationLayout.Row { assetbundle = entry.bundle, file = entry.asset, text = binding.text };
                var program = KksDialogueProgram.Generic(new KksConversationLayout.GenericInfo(binding.sourceRow, row), false);
                _machine.Start(program, outcome => { if (!_disposed && Active && ReferenceEquals(session, _native)) completed(); }, error =>
                {
                    if (_disposed || !Active || !ReferenceEquals(session, _native)) return;
                    StopPresentation(); Present("音声を再生できませんでした：" + error.GetBaseException().Message); completed();
                });
                return true;
            }
            catch (Exception error)
            {
                StopPresentation(); Present("音声を開始できませんでした：" + error.GetBaseException().Message); return false;
            }
        }
        private static string VoiceGroupName(string group)
        {
            switch (group)
            {
                case "Introduction": return "会話の導入";
                case "Talk": return "話す";
                case "Listen": return "聞く";
                case "See": return "見る・呼び掛けの反応";
                case "Touch": return "触れた時の反応";
                case "Interlude": return "会話の合間";
                case "LeaveAlone": return "待機中の反応";
                case "EndConversation": return "会話の終了";
                case "Event": return "誘い・イベントの反応";
                default: return group;
            }
        }
        private void ShowVoiceGroups()
        {
            if (_disposed || !Active || _native.Heroine.voiceNo != _characterVoices.personality) return;
            _placeholderVoice?.Dispose(); _placeholderVoice = null;
            Present("セクシーの会話用音声 " + _characterVoices.entries.Length + "件。元用途別に個別再生できます。再生だけで状態・関係値は変更しません。\n生活場面への割当ては別です。用途未確認の音声も資産名で残しています。");
            var choices = _characterVoices.Groups().Select(group => new KksConversationView.Choice(VoiceGroupName(group)
                + "（" + _characterVoices.InGroup(group).Count() + "件）", () => ShowVoicePage(group, default))).ToList();
            choices.Add(new KksConversationView.Choice("用途未確認（" + _characterVoices.InGroup("用途未確認").Count() + "件）", () => ShowVoicePage("用途未確認", default)));
            choices.Add(new KksConversationView.Choice("全音声（資産順）", () => ShowVoicePage(null, default)));
            choices.Add(new KksConversationView.Choice("会話メニューへ戻る", ShowMenu)); _view.SetChoices(choices);
        }
        private void ShowVoicePage(string group, int page)
        {
            if (_disposed || !Active) return;
            _placeholderVoice?.Dispose(); _placeholderVoice = null;
            var rows = _characterVoices.InGroup(group).OrderBy(entry => entry.id, StringComparer.Ordinal).ToArray();
            var offset = page * _characterVoices.pageSize; var session = _native;
            var choices = new List<KksConversationView.Choice>();
            Present((group == null ? "全音声" : VoiceGroupName(group)) + "／" + rows.Length + "件／ページ " + (page + _catalog.Runtime.one));
            foreach (var entry in rows.Skip(offset).Take(_characterVoices.pageSize))
            {
                var selected = entry;
                var caption = string.IsNullOrEmpty(entry.text) ? entry.asset + "（原文未確認）" : entry.text;
                choices.Add(new KksConversationView.Choice(caption, () =>
                {
                    if (_disposed || !Active || !ReferenceEquals(session, _native)) return;
                    _placeholderVoice?.Dispose(); _placeholderVoice = null;
                    Present((string.IsNullOrEmpty(selected.text) ? "原文未確認" : selected.text) + "\n音声：" + selected.asset
                        + "\n元用途：" + string.Join("、", selected.sources.Select(source => VoiceGroupName(source.group)).Distinct()));
                    try
                    {
                        _placeholderVoice = _voices.Play(new[] { new KksVoicePlayback.Clip { Bundle = selected.bundle, Asset = selected.asset } },
                            session.Heroine.voiceNo, session.Heroine.voicePitch, session.Actor, () => { }, error =>
                            { if (!_disposed && Active && ReferenceEquals(session, _native)) Present("音声再生失敗：" + error.GetBaseException().Message); });
                    }
                    catch (Exception error) { Present("音声再生失敗：" + error.GetBaseException().Message); }
                }));
            }
            choices.Add(new KksConversationView.Choice("前の音声", () => ShowVoicePage(group, page - _catalog.Runtime.one), page > default(int)));
            choices.Add(new KksConversationView.Choice("次の音声", () => ShowVoicePage(group, page + _catalog.Runtime.one), offset + _characterVoices.pageSize < rows.Length));
            choices.Add(new KksConversationView.Choice("音声を止める", () => { _placeholderVoice?.Dispose(); _placeholderVoice = null; }));
            choices.Add(new KksConversationView.Choice("元用途の一覧へ戻る", ShowVoiceGroups)); _view.SetChoices(choices);
        }
    }
}
