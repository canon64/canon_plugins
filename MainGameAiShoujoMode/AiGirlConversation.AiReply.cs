using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MainGameAiShoujoMode.Conversation;

namespace MainGameAiShoujoMode
{
    public sealed partial class AiGirlConversation
    {
        private AiConversationAdmission _aiAdmission;
        private AiConversationPipe _aiPipe;
        private AiReplySettings _aiSettings;
        private AiReplyResponse _aiResponse;
        private AiReplyAction[] _aiOffered;
        private string _aiTurnText;
        private bool _aiTestTurn;
        private SynchronizationContext _aiMain;
        private CancellationTokenSource _aiDeadline;
        private bool _aiNativeMenu;
        private bool _aiRecording, _aiCapturePending;
        private AiTurnActionResult _aiLastActionResult;
        private bool AiConversationOpen => _aiAdmission?.Open == true;
        internal Furniture.AiFurnitureSession Furniture {get;set;}
        private void CancelAiReply()
        {
            var admission = _aiAdmission; _aiAdmission = null;
            admission?.Close(); _aiDeadline?.Cancel(); _aiDeadline = null;
            var pipe = _aiPipe; _aiPipe = null; pipe?.Dispose();
            if (admission != null) AiConversationVoice.Cancel(admission.Conversation);
            _presentation?.RestoreExternalExpression();
            _aiResponse = null; _aiOffered = null; _view?.SetAiInput(false, false);
            _aiTurnText=null;_aiTestTurn=false;
            _aiRecording = _aiCapturePending = false;
        }
        private void AiConversationSettingChanged()
        { StopPresentation(); _aiNativeMenu = !Plugin.AiConversationEnabled; if (Active && !_disposed) ShowMenu(); }
        private void ShowAiReplyMenu() { _aiNativeMenu = false; CancelAiReply(); ShowMenu(); }
        private void ShowAiNativeMenu() { CancelAiReply(); _aiNativeMenu = true; StopPresentation(); ShowMenu(); }
        private void StartAiConversation()
        {
            if (AiConversationOpen || !Plugin.AiConversationEnabled || !Active || _disposed) return;
            try
            {
                // Stop any table playback while retaining the actor/camera presentation already
                // created by TryInteract. Releasing and rebuilding it here needlessly resets IK.
                _machine?.Stop(); _placeholderVoice?.Dispose(); _placeholderVoice = null; _voices.Stop(); EnsurePresentation();
                _aiMain = SynchronizationContext.Current ?? throw new InvalidOperationException("会話をメインスレッドから開始してください。");
                _aiSettings = AiReplySettings.Load(_contentDirectory);
                var admission = new AiConversationAdmission(Resident.ResidentId); _aiAdmission = admission;
                var pipe = new AiConversationPipe(_aiMain, _aiSettings.maxJsonBytes, _aiSettings.connectTimeoutMilliseconds, _aiSettings.nativePipe);
                _aiPipe = pipe; pipe.Received += ReceiveAiConversation;
                pipe.Disconnected += error => { if (ReferenceEquals(_aiAdmission, admission)) AiConnectionFailed(error); };
                AiStatus("Human_2に接続しています。");
                var hello = AiMessage("open"); hello.name = _characters.ConversationName;
                hello.instructions = File.ReadAllText(Path.Combine(Plugin.AiConversationMaterials, "ai-instructions.json"));
                hello.reference = File.ReadAllText(Path.Combine(Plugin.AiConversationMaterials, "ai-reference.json"));
                pipe.Connect(hello).ContinueWith(task => { if (task.IsFaulted) pipe.ReportFailure(task.Exception); }, TaskScheduler.Default);
                ArmAiDeadline();
            }
            catch (Exception error) { AiConnectionFailed(error.GetBaseException().Message); }
        }
        private AiConversationMessage AiMessage(string type) => new AiConversationMessage
        { type = type, conversation_id = _aiAdmission.Conversation, character_id = _aiAdmission.Character,
            request_id = _aiAdmission.Request, input_id = _aiAdmission.Input };
        private void SendAi(AiConversationMessage message)
        {
            var pipe = _aiPipe; if (pipe == null) return;
            pipe.Send(message).ContinueWith(task => { if (task.IsFaulted) pipe.ReportFailure(task.Exception); }, TaskScheduler.Default);
        }
        private void AiStatus(string text)
        {
            _view.PresentLine(_characters.ConversationName, text, addToLog: false); _view.SetAiInput(false, false);
            _view.SetChoices(new[] { new KksConversationView.Choice("通常の会話メニュー", ShowAiNativeMenu),
                new KksConversationView.Choice("会話をやめる", () => EndCore(true)) });
        }
        private void AiConnectionFailed(string error)
        {
            CancelAiReply(); if (!Active || _disposed) return;
            _view.PresentLine(_characters.ConversationName, "AI会話を接続できませんでした：" + error, addToLog: false);
            _view.SetChoices(new[] { new KksConversationView.Choice("AI会話へ再接続", ShowAiReplyMenu),
                new KksConversationView.Choice("通常の会話メニュー", ShowAiNativeMenu), new KksConversationView.Choice("会話をやめる", () => EndCore(true)) });
        }
        private void ReadyAiInput(string status = null)
        {
            if (!AiConversationOpen) return;
            _aiDeadline?.Cancel(); _aiDeadline = null; _aiResponse = null; _aiAdmission.Ready();
            _aiRecording = _aiCapturePending = false;
            _presentation?.RestoreExternalExpression();
            if (status != null) _view.PresentLine(_characters.ConversationName, status, addToLog: false);
            _view.ClearAiInput(); _view.SetAiInput(true, true);
            ShowAiInputChoices();
            SendAi(AiMessage("input_ready"));
        }
        private void ShowAiInputChoices()
        {
            _view.SetChoices(new[]
            {
                new KksConversationView.Choice("文字を送信", () => AcceptAiInput(_aiAdmission.Input, _view.AiInputText, false)),
                new KksConversationView.Choice(_aiCapturePending ? "マイクを開始しています…" : _aiRecording ? "終了（音声を送信）" : "音声開始", ToggleAiRecording, !_aiCapturePending),
                new KksConversationView.Choice("通常の会話メニュー", ShowAiNativeMenu),
                new KksConversationView.Choice("会話をやめる", () => EndCore(true))
            });
        }
        private void ToggleAiRecording()
        {
            if (!AiConversationOpen || !Active || _disposed || _aiAdmission.State != "input" || _aiCapturePending) return;
            if (_aiRecording) { AcceptAiInput(_aiAdmission.Input, null, true); return; }
            _aiCapturePending = true; ShowAiInputChoices();
            SendAi(AiMessage("capture_start")); ArmAiDeadline();
        }
        private void ReceiveAiConversation(AiConversationMessage message)
        {
            if (!AiConversationOpen || !Active || !Plugin.AiConversationEnabled || Resident == null || !Resident.Conversing || Resident.InH
                || message.conversation_id != _aiAdmission.Conversation || message.character_id != _aiAdmission.Character) return;
            if (message.type == "ready" && _aiAdmission.State == "preparing") { ReadyAiInput("音声は「音声開始」で録音し、同じボタンの「終了」で送信します。"); return; }
            if (message.type == "capture_started" && _aiAdmission.State == "input" && _aiCapturePending && message.input_id == _aiAdmission.Input)
            {
                _aiDeadline?.Cancel(); _aiDeadline = null; _aiCapturePending = false; _aiRecording = true;
                _view.PresentLine(_characters.ConversationName, "録音中です。「終了」で音声を送信します。", addToLog: false);
                ShowAiInputChoices(); return;
            }
            if (message.type == "notice" && _aiAdmission.State == "input")
            {
                if (_aiCapturePending) { _aiDeadline?.Cancel(); _aiDeadline = null; _aiCapturePending = false; _aiRecording = false; ShowAiInputChoices(); }
                _view.PresentLine(_characters.ConversationName, message.error, addToLog: false); return;
            }
            if (message.type == "test_reply")
            {
                try
                {
                    var reply = AiReplyResponse.Parse(message.payload, _aiSettings);
                    var offered = AiReplyActions(includeAllItems:true).Where(action=>action.Matches(reply)).ToArray();
                    if (!offered.Any(action => action.Matches(reply)) || !_aiAdmission.Accept(message.input_id))
                        throw new InvalidOperationException("この受付ではテスト返答を受け取れません。");
                    _aiOffered = offered;_aiTestTurn=true;_aiTurnText=null; AiStatus("返答JSONを検証しています。"); _aiAdmission.Move("waiting");
                    var test = AiMessage("test_accepted"); test.text = message.payload;
                    test.payload = AiConversationJson.Write(new AiTurn { player_message = string.Empty,
                        character_id = Resident.ResidentId, available_actions = AiConversationItemOffers.ForWire(offered) }); SendAi(test); ArmAiDeadline();
                }
                catch (Exception error)
                { var rejected = AiMessage("rejected"); rejected.error = error.GetBaseException().Message; SendAi(rejected); }
                return;
            }
            if (message.type == "input" || message.type == "voice_input")
            {
                if (!AcceptAiInput(message.input_id, message.text, message.type == "voice_input"))
                { var rejected = AiMessage("rejected"); rejected.error = "現在は入力を受け付けていません。"; SendAi(rejected); }
                return;
            }
            if (!_aiAdmission.Matches(message)) return;
            try
            {
                if (message.type == "transcribed" && _aiAdmission.State == "sending")
                {
                    if (string.IsNullOrWhiteSpace(message.text) || message.text.Length > _aiSettings.maxTextCharacters)
                        throw new InvalidDataException("音声から送信する文章を取得できませんでした。");
                    SendAiTurn(message.text); return;
                }
                if (message.type == "reply" && _aiAdmission.State == "waiting")
                {
                    var reply = AiReplyResponse.Parse(message.payload, _aiSettings);
                    if (!_aiOffered.Any(action => action.Matches(reply))) throw new InvalidDataException("提示していない行動・対象が返されました。");
                    _aiResponse = reply; _aiAdmission.Move("audio"); _view.PresentLine(_characters.ConversationName, reply.text);
                    var synthesis = AiMessage("synthesize"); synthesis.text = reply.text; SendAi(synthesis); ArmAiDeadline(); return;
                }
                if (message.type == "audio_ready" && _aiAdmission.State == "audio")
                {
                    _aiDeadline?.Cancel(); var owned = _aiAdmission;
                    var accepted = AiConversationVoice.TryPlay(owned.Conversation, owned.Request,
                        _native.Actor, message.audio_path, (conversation, request, status) =>
                        {
                            if (!ReferenceEquals(_aiAdmission, owned) || !owned.Open || owned.Request != request || owned.Conversation != conversation) return;
                            if (status == "started") { owned.Move("speaking"); _presentation.ApplyExternalExpression(_aiSettings.emotions.Single(e => e.key == _aiResponse.emotion).expression); }
                            else if (status == "completed") FinishAiTurn(); else AiAudioFailed("音声再生が中断または失敗しました。");
                        });
                    if (!accepted) AiAudioFailed("音声を再生できません。別の音声が使用中か、VoiceFaceが利用できません。");
                    return;
                }
                if (message.type == "error")
                { if (_aiResponse != null) AiAudioFailed(message.error); else ReadyAiInput("送信に失敗しました：" + message.error); }
            }
            catch (Exception error)
            { SendAi(AiMessage("cancel_request")); ReadyAiInput("返答を適用できませんでした：" + error.GetBaseException().Message); }
        }
        private bool AcceptAiInput(string input, string text, bool voice)
        {
            if (!AiConversationOpen || (voice && !_aiRecording) || (!voice && (string.IsNullOrWhiteSpace(text) || text.Length > _aiSettings.maxTextCharacters)) || !_aiAdmission.Accept(input)) return false;
            _aiRecording = _aiCapturePending = false;
            AiStatus(voice ? "音声を文字にしています。" : "返答を待っています。");
            SendAi(AiMessage(voice ? "capture_finish" : "accepted")); ArmAiDeadline();
            if (!voice) SendAiTurn(text); return true;
        }
        [DataContract] private sealed class AiTurn
        {
            [DataMember] internal string player_message, state_revision, character_id, character_name, location_id;
            [DataMember] internal int personality;
            [DataMember] internal AiTurnState state;
            [DataMember] internal AiTurnRelationship relationship;
            [DataMember] internal AiReplyAction[] available_actions;
            [DataMember(EmitDefaultValue = false)] internal AiTurnActionResult previous_action_result;
        }
        [DataContract] private sealed class AiTurnActionResult
        {
            [DataMember] internal string action, target_id, message;
            [DataMember] internal bool success;
        }
        [DataContract] private sealed class AiTurnState
        {
            [DataMember] internal float health, fullness, fatigue, mood, motivation, minimum, maximum;
            [DataMember] internal string illness, current_action, location_id;
        }
        [DataContract] private sealed class AiTurnRelationship
        {
            [DataMember] internal bool friend, girlfriend;
            [DataMember] internal int friendship, favor;
        }
        private void SendAiTurn(string text)
        {
            ClockSync?.Invoke();_aiTurnText=text;_aiTestTurn=false; _aiOffered = AiReplyActions(text).ToArray(); var packet = AiMessage("turn"); packet.text = text;
            var state = Resident.State; var location = P.T("sandbox.map");
            packet.payload = AiConversationJson.Write(new AiTurn { player_message = text, state_revision = Life.Store.Read().revision,
                character_id = Resident.ResidentId, character_name = _characters.ConversationName, personality = _native.Heroine.voiceNo,
                location_id = location, state = new AiTurnState { health = state.health, fullness = state.fullness, fatigue = state.fatigue,
                    mood = state.mood, motivation = state.motivation, minimum = Resident.Rules.minimum, maximum = Resident.Rules.maximum,
                    illness = state.sickness == Resident.Rules.healthySickness ? null : state.sickness == Resident.Rules.coldSickness ? "cold" : "unknown",
                    current_action = state.action, location_id = location }, available_actions = AiConversationItemOffers.ForWire(_aiOffered),
                relationship = new AiTurnRelationship { friend = _native.Heroine.isFriend, girlfriend = _native.Heroine.isGirlfriend,
                    friendship = state.friendship, favor = _native.Heroine.favor }, previous_action_result = _aiLastActionResult });
            _view.AppendChoice("あなた", text); _aiAdmission.Move("waiting"); SendAi(packet); ArmAiDeadline();
        }
        private IEnumerable<AiReplyAction> AiReplyActions(string itemRequest=null,bool includeAllItems=false)
        {
            yield return new AiReplyAction { Action = "talk", Label = "その場で話す・質問する・断る" };
            yield return new AiReplyAction { Action = "normal", Label = "同行を終えて通常の生活へ戻る" };
            if (Resident.State.sickness == Resident.Rules.healthySickness)
            {
                yield return new AiReplyAction { Action = "follow", Target = "player", Label = "プレイヤーについていく" };
                if (RequestH != null) yield return new AiReplyAction { Action = "h", Target = "player", Label = "プレイヤーとHを始める（実行時に本編Hへ移行）" };
            }
            var mentioned=AiConversationItemOffers.MentionedNames(itemRequest,Life.Items.items.Select(item=>item.name));
            foreach (var stock in Life.Store.Read().inventories.Where(value => value.Key == Life.PlayerInventoryKey)
                .SelectMany(value => value.items).Where(value => value.Count > default(int)).GroupBy(value => new { value.CategoryID, value.ID }))
            {
                var definition = Life.Items.Find(stock.Key.CategoryID, stock.Key.ID); if (definition == null) continue;
                if(!includeAllItems&&!mentioned.Contains(definition.name,StringComparer.Ordinal))continue;
                yield return new AiReplyAction { Action = "receive_item", Target = "item:" + Life.PlayerInventoryKey + ":" + stock.Key.CategoryID + ":" + stock.Key.ID,
                    Inventory = Life.PlayerInventoryKey, Category = stock.Key.CategoryID, Item = stock.Key.ID,
                    Label = definition.name };
            }
            if (Life.Activities == null) yield break;
            yield return new AiReplyAction { Action = "rest" };
            if(Furniture!=null)foreach(var bed in Furniture.AvailableBeds(Resident))yield return new AiReplyAction{Action="rest",Target=bed.Target,Label=bed.Name+"で休む"};
            foreach (var inventory in Life.Store.Read().inventories.Where(v => v.Key == "agent:" + Resident.ResidentId))
                foreach (var stock in inventory.items.Where(s => s.Count > default(int) && Life.Items.Find(s.CategoryID, s.ID)?.food == true))
                    yield return new AiReplyAction { Action = "eat", Target = "item:" + inventory.Key + ":" + stock.CategoryID + ":" + stock.ID,
                        Inventory = inventory.Key, Category = stock.CategoryID, Item = stock.ID,
                        Label = Life.Items.Find(stock.CategoryID, stock.ID).name };
        }
        private void ArmAiDeadline()
        {
            _aiDeadline?.Cancel(); var deadline = _aiDeadline = new CancellationTokenSource(); var admission = _aiAdmission;
            Task.Delay(TimeSpan.FromSeconds(_aiSettings.timeoutSeconds), deadline.Token).ContinueWith(task =>
            {
                if (task.IsCanceled) return;
                _aiMain.Post(_ =>
                {
                    if (deadline.IsCancellationRequested || !ReferenceEquals(admission, _aiAdmission)) return;
                    if (_aiAdmission.State == "preparing") { AiConnectionFailed("Human_2の受付から応答がありません。"); return; }
                    SendAi(AiMessage("cancel_request"));
                    if (_aiResponse != null) AiAudioFailed("音声の準備が時間内に終わりませんでした。"); else ReadyAiInput("返答が時間内に届きませんでした。");
                }, null);
            }, TaskScheduler.Default);
        }
        private void AiAudioFailed(string reason)
        {
            if (!AiConversationOpen || _aiResponse == null) return;
            _aiDeadline?.Cancel(); _aiAdmission.Move("audio_failed");
            _view.SetChoices(new[] { new KksConversationView.Choice(reason ?? "音声を再生できませんでした。", () => { }, false),
                new KksConversationView.Choice("音声なしで続ける", FinishAiTurn),
                new KksConversationView.Choice("行動を取り消して入力へ戻る", () => ReadyAiInput()),
                new KksConversationView.Choice("会話をやめる", () => EndCore(true)) });
        }
        private void FinishAiTurn()
        {
            if (!AiConversationOpen || _aiResponse == null) return;
            var reply = _aiResponse; _aiResponse = null; var operation = _aiAdmission.Request; _aiAdmission.Move("executing");
            _presentation?.RestoreExternalExpression(); var action = AiReplyActions(_aiTurnText,_aiTestTurn).SingleOrDefault(value => value.Matches(reply));
            if (action == null)
            {
                _aiLastActionResult = new AiTurnActionResult { action = reply.action, target_id = reply.target_id, success = false, message = "現在の体調・在庫ではこの行動を実行できません。" };
                ReadyAiInput(_aiLastActionResult.message); return;
            }
            SendAi(AiMessage("turn_finished"));
            try
            {
                var result = AiConversationActionExecution.Execute(action, operation, Resident, Life, _catalog.Runtime.one,
                    RequestH == null ? (Action)null : BeginHTransition, () => EndCore(true));
                _aiLastActionResult = reply.action == "talk" ? _aiLastActionResult : new AiTurnActionResult
                    { action = reply.action, target_id = reply.target_id, success = true, message = result };
                if (AiConversationOpen) ReadyAiInput(reply.action == "receive_item" ? result : null);
            }
            catch (Exception error)
            {
                _aiLastActionResult = new AiTurnActionResult { action = reply.action, target_id = reply.target_id, success = false, message = error.GetBaseException().Message };
                if (AiConversationOpen) ReadyAiInput("行動を実行できませんでした：" + _aiLastActionResult.message);
                else if (Active) AiConnectionFailed("行動を実行できませんでした：" + _aiLastActionResult.message);
            }
        }
    }
}
