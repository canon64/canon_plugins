using System;
using MainGameAiShoujoMode.Conversation;
using MainGameAiShoujoMode.Life;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using MapEnvironmentRuntime;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MainGameAiShoujoMode
{
    public sealed partial class AiGirlConversation : IDisposable
    {
        private SandboxHost _host;
        private AiAnimationTrial _characters;
        private Action _changed, _resume;
        private Action<string> _log;
        private KksConversationView _view;
        private KksConversationCatalog _catalog;
        private KksConversationStateStore _store;
        private KksVoicePlayback _voices;
        private readonly KksVoiceBundleContext _voiceBundles = new KksVoiceBundleContext();
        private KksNativeConversationSession _native;
        private KksDialoguePresentation _presentation;
        private KksDialogueMachine _machine;
        private KksConversationSelector _selector;
        private Func<ChaControl> _player;
        private Func<IDisposable> _suspendMusic;
        private string _contentDirectory;
        private EventSystem _events;
        private bool _disposed;
        public Action RequestH { get; set; }
        internal void AttachAuthoring(Authoring.EventAuthoringEditor editor)
        { if (_view != null) _view.EditRequested = (name, text) => editor.CaptureNative(_view, name, text); }
        internal Func<string> PlacePickup { get; set; }
        internal Func<string> PickupStatus { get; set; }
        internal AiLifeRuntime Life { get; set; }
        internal AiLifeController Resident { get; private set; }
        internal Action<AiLifeController> ResidentBound { get; set; }
        internal Action ClockSync { get; set; }
        public bool Active => _view != null && _view.Active;
        public Rect InputRect => new Rect(P.N("math.zero"), P.N("math.zero"), Screen.width, Screen.height);
        private Parameters P => _host.Session.Settings;
        public void Initialize(SandboxHost host, AiAnimationTrial characters, Func<ChaControl> player, string contentDirectory, Action onChanged, Action<string> log, Func<IDisposable> suspendMusic, string restoredSavePath = null)
        {
            _host = host; _characters = characters; _changed = onChanged; _log = log; _player = player;
            _contentDirectory = contentDirectory; _suspendMusic = suspendMusic ?? throw new ArgumentNullException(nameof(suspendMusic));
            _catalog = new KksConversationCatalog(Path.Combine(contentDirectory, "kks-conversation-catalog.json"));
            _store = new KksConversationStateStore(restoredSavePath ?? _catalog.Runtime.savePath, _catalog.Runtime.schema);
            _voices = new KksVoicePlayback(host, _catalog.Runtime, log);
            _characterVoices = KksCharacterVoiceCatalog.Load(Path.Combine(contentDirectory, "kks-character-voices.json"));
            _voices.Catalog = _characterVoices;
            _view = new KksConversationView(host.transform, contentDirectory);
            Plugin.AiConversationSettingChanged += AiConversationSettingChanged;
            _characters.ConversationInvalidated += Invalidated;
            _characters.ActorChanged += BindActor;
            Application.focusChanged += FocusChanged;
            BindActor(_characters.CurrentActor);
        }
        private void BindActor(ChaControl actor)
        {
            if (AiConversationOpen) EndCore(false);
            Resident?.Connections.Dispose();
            ResidentBound?.Invoke(null); Resident = null;
            if (_disposed || actor == null) return;
            try
            {
                var native = new KksNativeConversationSession(actor, _player(), _characters.ConversationIdentityKey, _catalog, _store);
                Resident = new AiLifeController(Life, native.Identity, _characters.ConversationIdentityKey);
                ResidentBound?.Invoke(Resident);
            }
            catch (Exception error) { _log?.Invoke("AI_LIFE_BIND_ERROR " + error); }
        }
        public bool TryInteract()
        {
            if (_disposed) return false;
            if (Active) return true;
            var camera = _host.Walker?.ViewCamera; var target = _characters.ConversationTarget;
            if (camera == null || target == null || _host.Session.Panel || _host.Session.EditingParameter) return false;
            var point = target.position + Vector3.up * P.N("conversation.targetHeight");
            var offset = point - camera.transform.position;
            if (offset.magnitude > P.N("conversation.reach") || Vector3.Angle(camera.transform.forward, offset) > P.N("conversation.viewAngle")) return false;
            var player = _host.Walker;
            foreach (var hit in Physics.RaycastAll(camera.transform.position, offset.normalized, offset.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (player != null && Physics.GetIgnoreLayerCollision(player.gameObject.layer, hit.collider.gameObject.layer)) continue;
                if (!hit.transform.IsChildOf(target) && (player == null || !hit.transform.IsChildOf(player.transform))) return false;
            }
            try
            {
                _native = new KksNativeConversationSession(_characters.CurrentActor, _player(), _characters.ConversationIdentityKey, _catalog, _store);
                Plugin.RecordEventPositions(() => EventPositionSnapshot.Capture("conversation.begin", _host,
                    _player()?.transform, target, _native.Identity));
                if (Life == null) throw new InvalidOperationException("AI life state is not connected to the conversation.");
                Life.BindResident(_native.Identity);
                if (Resident == null || Resident.ResidentId != _native.Identity) BindActor(_characters.CurrentActor);
                if (Resident == null) throw new InvalidOperationException("女の子の状態管理を開始できません。");
                ClockSync?.Invoke();
                Resident.BeginConversation();
                _aiNativeMenu = false;
                if (_native.Profile.Source.conversation.className != nameof(CommunicationInfo)) throw new NotSupportedException("固定キャラ用の会話進行は未接続です。");
                _selector = new KksConversationSelector(_catalog.NormalRules);
                _resume = _characters.PauseForConversation(camera.transform);
                EnsurePresentation();
                if (EventSystem.current == null && _events == null)
                {
                    var events = new GameObject("AI Conversation UI Events", typeof(EventSystem), typeof(StandaloneInputModule)); events.transform.SetParent(_view.Transform, false); _events = events.GetComponent<EventSystem>();
                }
                _view.Show(_characters.ConversationName, Resident.State.sickness == Resident.Rules.healthySickness ? "どうしたの？" : "少し具合が悪いの……");
                // AI input must open on interaction, before a native greeting waits for advance/focus.
                if (Plugin.AiConversationEnabled || Resident.State.sickness != Resident.Rules.healthySickness || !TryPlayBoundVoice("greeting", ShowMenu)) ShowMenu();
                KksConversationDiagnostics.KsPlugContext("begin", _log);
                _changed?.Invoke(); _log?.Invoke("AI_GIRL_CONVERSATION_BEGIN nativeUi=true resident=" + _native.Identity); return true;
            }
            catch (Exception ex) { _log?.Invoke("AI_GIRL_CONVERSATION_ERROR " + ex); EndCore(true); return true; }
        }
        public void Draw() { }
        private void BeginH()
        {
            if (!TryPlayBoundVoice("h.accept", BeginHCore)) BeginHCore();
        }
        private void BeginHCore()
        {
            if (_disposed || !Active) return;
            if (RequestH == null) { Present("Hへの移行処理が準備されていません。"); return; }
            try { BeginHTransition(); }
            catch (Exception ex) { _log?.Invoke("AI_GIRL_CONVERSATION_H_ERROR " + ex); if (Active) Present(ex.GetBaseException().Message); }
        }
        private void BeginHTransition()
        {
            if (_disposed || !Active || RequestH == null) throw new InvalidOperationException("Hへの移行を開始できません。");
            StopPresentation(); RequestH();
        }
        public Action SuspendForH()
        {
            if (_disposed || !Active) throw new InvalidOperationException("会話中の女の子がいません。");
            var continuation = _resume; _resume = null;
            var resident = Resident; resident?.BeginH();
            StopPresentation();
            _view.Hide(); _changed?.Invoke(); _log?.Invoke("AI_GIRL_CONVERSATION_SUSPEND_H");
            bool completed = false;
            return () =>
            {
                if (completed) return; completed = true;
                if (!_disposed) { try { continuation?.Invoke(); } finally { resident?.EndH(); } }
            };
        }
        private void ShowMenu()
        {
            if (Plugin.AiConversationEnabled && !_aiNativeMenu) { StartAiConversation(); return; }
            CancelAiReply();
            _presentation?.FocusConversationCamera();
            _view.AdvanceRequested = null; _view.SetCanAdvance(false);
            var state = Resident.State;
            var menu = new List<KksConversationView.Choice>
            {
                new KksConversationView.Choice("トーク", ShowTalk),
                new KksConversationView.Choice("アドバイスする", ShowAdvice),
                new KksConversationView.Choice("アイテムを渡す", () => ShowItems(false)),
                new KksConversationView.Choice("薬をあげる", () => ShowItems(true))
            };
            if (Plugin.AiConversationEnabled) menu.Add(new KksConversationView.Choice("AIと会話する", ShowAiReplyMenu));
            menu.Add(state.action == "follow"
                ? new KksConversationView.Choice("同行をやめる", () => LifeCommand("normal", "ここで別れましょう。", true))
                : new KksConversationView.Choice("ついてきて", RequestFollow));
            menu.Add(new KksConversationView.Choice("Hする", BeginH, state.sickness == Resident.Rules.healthySickness));
            if (CanTradeNativeMerchant?.Invoke() == true) menu.Add(new KksConversationView.Choice("品物を交換する", ShowNativeMerchant));
            menu.Add(new KksConversationView.Choice("状態を見る・確認操作", ShowStatus));
            menu.Add(new KksConversationView.Choice("接続確認・仮の拾得物", ShowConnections));
            menu.Add(new KksConversationView.Choice("着替えを渡す・所持を見る", ShowOutfits));
            menu.Add(new KksConversationView.Choice("セクシーの音声・元用途一覧", ShowVoiceGroups, _native.Heroine.voiceNo == _characterVoices.personality));
            menu.Add(new KksConversationView.Choice("別れる", End));
            _view.SetChoices(menu);
        }
        private void ShowTalk() => _view.SetChoices(new[]
        {
            new KksConversationView.Choice("話す", () => Select(KksConversationLayout.Group.Talk)),
            new KksConversationView.Choice("聞く", () => Select(KksConversationLayout.Group.Listen)),
            new KksConversationView.Choice("戻る", ShowMenu)
        });
        private void ShowAdvice() => _view.SetChoices(new[]
        {
            new KksConversationView.Choice("休んで（地面にしゃがむ）", () => QueueLifeActivity("rest")),
            new KksConversationView.Choice("食事をお願いする（食べ物を選ぶ）", () => ShowActivityFoods()),
            new KksConversationView.Choice("生活動作を中止する", CancelLifeActivity, Resident.State.activity != null),
            new KksConversationView.Choice("原本の生活動作・素材を選ぶ", ShowSourceMotionGroups),
            new KksConversationView.Choice("作業をお願いする（動作を選ぶ・成果は未指定）", ShowSourceMotionGroups),
            new KksConversationView.Choice("頼まれもの（仮の入口）", () => RunConnectionKey("quest")),
            new KksConversationView.Choice("成長イベント（仮の入口）", () => RunConnectionKey("growth")),
            new KksConversationView.Choice("戻る", ShowMenu)
        });
        private void LifeCommand(string command, string text, bool close)
        {
            try
            {
                Resident.Execute(command);
                Action finish = () => { if (close) EndCore(true); else { Present(text); ShowMenu(); } };
                if (!TryPlayBoundVoice(command == "follow" ? "follow.accept" : command == "normal" ? "bye" : command + ".accept", finish)) finish();
            }
            catch (Exception error) { Present(error.GetBaseException().Message); ShowMenu(); }
        }
        private void RequestFollow()
        {
            if (Resident.State.sickness != Resident.Rules.healthySickness)
            {
                Action refused = () => { Present("病気のため同行できません。"); ShowMenu(); };
                if (!TryPlayBoundVoice("follow.reject", refused)) refused();
                return;
            }
            LifeCommand("follow", "一緒に行きましょう。", true);
        }
        private void ShowStatus()
        {
            Present(Resident.Summary());
            _view.SetChoices(new[]
            {
                new KksConversationView.Choice("確認：健康に戻す", () => TestState("test:healthy")),
                new KksConversationView.Choice("確認：風邪にする", () => TestState("test:cold")),
                new KksConversationView.Choice("確認：空腹にする", () => TestState("test:hungry")),
                new KksConversationView.Choice("確認：作業中にする", () => TestState("test:work")),
                new KksConversationView.Choice("確認：同行にする", () => TestState("follow")),
                new KksConversationView.Choice("確認：風邪薬を所持品に追加", AddTestMedicine),
                new KksConversationView.Choice("確認：食べ物を所持品に追加", AddTestActivityFood),
                new KksConversationView.Choice("生活動作を中止する", CancelLifeActivity, Resident.State.activity != null),
                new KksConversationView.Choice("確認：ゲーム時間を進める", () => { Resident.Advance(Resident.Rules.restSeconds); ShowStatus(); }),
                new KksConversationView.Choice("戻る", ShowMenu)
            });
        }
        private void TestState(string command) { Resident.Execute(command); ShowStatus(); }
        private void ShowConnections()
        {
            if (_disposed || !Active) return;
            var choices = new List<KksConversationView.Choice>();
            foreach (var entry in Life.ConnectionCatalog.entries)
            {
                var request = entry; var operation = Guid.NewGuid().ToString("N"); var owner = Resident;
                choices.Add(new KksConversationView.Choice(request.label, () => RunConnection(owner, request, operation)));
            }
            choices.Add(new KksConversationView.Choice("女の子の状態・在庫を確認", ShowConnectionState));
            choices.Add(new KksConversationView.Choice("所持品を女の子へ渡す（追加効果なし）", ShowTransferItems));
            choices.Add(new KksConversationView.Choice("仮拾得物を目の前に置く", () =>
            {
                try { Present(PlacePickup?.Invoke() ?? "配置の入口がありません。"); }
                catch (Exception error) { Present(error.GetBaseException().Message); }
            }));
            choices.Add(new KksConversationView.Choice("戻る", ShowMenu)); _view.SetChoices(choices);
        }
        private void RunConnectionKey(string key)
        {
            var entry = Life.ConnectionCatalog.entries.Single(value => value.key == key);
            RunConnection(Resident, entry, Guid.NewGuid().ToString("N"));
        }
        private void ShowTransferItems()
        {
            var owner = Resident; var choices = new List<KksConversationView.Choice>();
            foreach (var stock in Life.Store.Read().inventories.Single(value => value.Key == Life.PlayerInventoryKey).items)
            {
                var definition = Life.Items.Find(stock.CategoryID, stock.ID); if (definition == null) continue;
                var request = new AiLifeConnection { key = "item:transfer", kind = "transfer", category = stock.CategoryID, item = stock.ID, count = _catalog.Runtime.one };
                var operation = Guid.NewGuid().ToString("N");
                choices.Add(new KksConversationView.Choice(definition.name + " ×" + stock.Count, () => RunConnection(owner, request, operation)));
            }
            Present("品物を一つ、女の子の在庫へ移します。回復・好感度などの追加効果はありません。" + (choices.Any() ? "" : "\n渡せる所持品がありません。"));
            choices.Add(new KksConversationView.Choice("取消・接続確認へ戻る", ShowConnections)); _view.SetChoices(choices);
        }
        private void RunConnection(AiLifeController owner, AiLifeConnection request, string operation)
        {
            if (_disposed || !Active || !ReferenceEquals(owner, Resident) || !owner.Conversing) return;
            try
            {
                var result = owner.Connections.Execute(request, operation);
                var text = (result.Completed ? result.Replayed ? "【確定済み】" : "【反映済み】" : "【未接続の入口】") + result.Message;
                Action finish = () => { Present(text); ShowConnectionReturn(); };
                var key = request.kind == "action" ? request.resource + ".accept" : request.kind == "outfit" ? "outfit.give"
                    : request.kind == "transfer" ? "gift" : request.key;
                if (!result.Replayed && TryPlayBoundVoice(key, finish)) return;
                finish(); return;
            }
            catch (Exception error) { Present(error.GetBaseException().Message); }
            ShowConnectionReturn();
        }
        private void ShowConnectionReturn()
        {
            _view.SetChoices(new[] { new KksConversationView.Choice("状態と在庫を見る", ShowConnectionState),
                new KksConversationView.Choice("接続確認へ戻る", ShowConnections), new KksConversationView.Choice("会話メニューへ戻る", ShowMenu) });
        }
        private void ShowConnectionState()
        {
            var world = Life.Store.Read();
            string Stock(string key) => string.Join("、", world.inventories.Single(value => value.Key == key).items.Select(value =>
                (Life.Items.Find(value.CategoryID, value.ID)?.name ?? value.CategoryID + ":" + value.ID) + " ×" + value.Count));
            Present(Resident.Summary() + "\nあなたの在庫：" + Stock(Life.PlayerInventoryKey)
                + "\n女の子の在庫：" + Stock("agent:" + Resident.ResidentId)
                + "\n渡した着替え：" + string.Join("、", Resident.State.outfits.Select(Path.GetFileName))
                + "\n拾得：" + PickupStatus?.Invoke());
            _view.SetChoices(new[] { new KksConversationView.Choice("接続確認へ戻る", ShowConnections), new KksConversationView.Choice("会話メニューへ戻る", ShowMenu) });
        }
        private int _outfitPage;
        private string[] _outfitFiles;
        private void ShowOutfits()
        {
            if (_disposed || !Active) return;
            var choices = new List<KksConversationView.Choice>();
            var owner = Resident;
            try
            {
                var directory = Path.GetFullPath(P.T("pod.coordinate.directory"));
                if (_outfitFiles == null) _outfitFiles = Directory.Exists(directory)
                    ? Directory.EnumerateFiles(directory, "*.png", SearchOption.AllDirectories).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray() : Array.Empty<string>();
                var size = Life.ConnectionCatalog.outfitPageSize;
                foreach (var path in _outfitFiles.Skip(_outfitPage * size).Take(size))
                {
                    var file = path; var operation = Guid.NewGuid().ToString("N");
                    choices.Add(new KksConversationView.Choice("渡す：" + file.Substring(directory.TrimEnd(Path.DirectorySeparatorChar).Length).TrimStart(Path.DirectorySeparatorChar), () =>
                    {
                        if (_disposed || !Active || !ReferenceEquals(owner, Resident)) return;
                        try
                        {
                            var coordinate = new ChaFileCoordinate();
                            if (!coordinate.LoadFile(file)) throw new InvalidDataException("KKSコーデカードを読み込めません。");
                            RunConnection(owner, new AiLifeConnection { key = "outfit:give", kind = "outfit", resource = Path.GetFullPath(file) }, operation);
                        }
                        catch (Exception error) { Present(error.GetBaseException().Message); }
                    }));
                }
                Present("コーデカードの参照を女の子の着替え一覧へ渡します。元ファイルは移動せず、今の服装も変更しません。\n所持：" + string.Join("、", owner.State.outfits.Select(Path.GetFileName))
                    + (choices.Any() ? "" : "\n指定フォルダーにコーデカードがありません。"));
                choices.Add(new KksConversationView.Choice("前の着替え", () => { _outfitPage -= _catalog.Runtime.one; ShowOutfits(); }, _outfitPage > default(int)));
                choices.Add(new KksConversationView.Choice("次の着替え", () => { _outfitPage += _catalog.Runtime.one; ShowOutfits(); }, (_outfitPage + _catalog.Runtime.one) * size < _outfitFiles.Length));
                choices.Add(new KksConversationView.Choice("着替え一覧を更新", () => { _outfitFiles = null; _outfitPage = default; ShowOutfits(); }));
            }
            catch (Exception error) { Present(error.GetBaseException().Message); }
            choices.Add(new KksConversationView.Choice("取消・戻る", ShowMenu)); _view.SetChoices(choices);
        }
        private void AddTestMedicine()
        {
            var request = new AiLifeStateStore.Request { id = Guid.NewGuid().ToString("N"), action = "acquire",
                destination = Life.PlayerInventoryKey, category = Resident.Rules.coldMedicineCategory, item = Resident.Rules.coldMedicineItem, count = _catalog.Runtime.one };
            try { Life.Store.Commit(Life.Store.Read(), request, out _, out _); Present("確認用の風邪薬を追加しました。"); }
            catch (Exception error) { Present(error.GetBaseException().Message); }
        }
        private void ShowItems(bool medicine)
        {
            var items = Life.Store.Read().inventories.Single(value => value.Key == Life.PlayerInventoryKey).items;
            var choices = new List<KksConversationView.Choice>();
            foreach (var stack in items.GroupBy(value => new { value.CategoryID, value.ID }))
            {
                var category = stack.Key.CategoryID; var item = stack.Key.ID;
                if (medicine && (category != Resident.Rules.coldMedicineCategory || item != Resident.Rules.coldMedicineItem)) continue;
                var definition = Life.Items?.Find(category, item);
                if (!medicine && (definition == null || !Life.Items.CanPresent(category, item))) continue;
                var operation = Guid.NewGuid().ToString("N"); var snapshot = Life.Store.Read();
                choices.Add(new KksConversationView.Choice((definition?.name ?? "風邪薬") + " ×" + stack.Sum(value => value.Count), () =>
                {
                    try
                    {
                        Resident.Give(snapshot, operation, category, item, _catalog.Runtime.one, medicine);
                        Action finish = () => { Present(medicine ? "薬を渡しました。" : "品物を渡しました。"); ShowMenu(); };
                        if (!TryPlayBoundVoice(medicine ? "medicine" : "gift", finish)) finish();
                    }
                    catch (Exception error) { Present(error.GetBaseException().Message); ShowMenu(); }
                }));
            }
            if (!choices.Any()) Present(medicine ? "渡せる風邪薬がありません。状態確認から試験用の薬を追加できます。" : "渡せるアイテムがありません。");
            choices.Add(new KksConversationView.Choice("取消", ShowMenu)); _view.SetChoices(choices);
        }
        private IDisposable _placeholderVoice;
        private void EnsurePresentation()
        {
            if (_presentation != null) return;
            var session = _native;
            _presentation = new KksDialoguePresentation(session, _view, _voices, _voiceBundles, PlaybackFailed, _log,
                actor => KksAdvNativeSession.Create(_host, _host.Walker.ViewCamera, session, actor, _view, _voices, _voiceBundles, _store, _contentDirectory, _suspendMusic, _log));
            _machine = new KksDialogueMachine(_presentation, _catalog.Runtime.zero);
        }
        private void Select(KksConversationLayout.Group group)
        {
            if (_disposed || !Active || _machine?.Active == true) return;
            _placeholderVoice?.Dispose(); _placeholderVoice = null;
            try
            {
                EnsurePresentation();
                var block = _selector.Select(_native.Profile.Layout, _native.Stage, group, null, _native.Context(), count => UnityEngine.Random.Range(_catalog.Runtime.zero, count));
                var operation = Guid.NewGuid().ToString("N"); var program = _native.Build(block);
                _log?.Invoke("KKS_CONVERSATION_PLAY resident=" + _native.Identity + " group=" + group + " source=" + _native.Profile.Source.conversation.asset + " row=" + block.row);
                _machine.Start(program, outcome =>
                {
                    _native.Commit(operation, block, outcome);
                    _log?.Invoke("KKS_CONVERSATION_COMMIT operation=" + operation + " favor=" + outcome.Favor + " lewdness=" + outcome.Lewdness);
                    ShowMenu();
                }, PlaybackFailed);
            }
            catch (Exception exception) { PlaybackFailed(exception); }
        }
        private void StopPresentation()
        {
            CancelAiReply();
            var machine = _machine; var presentation = _presentation; _machine = null; _presentation = null;
            try { machine?.Dispose(); } finally { presentation?.Dispose(); }
        }
        private void PlaybackFailed(Exception exception)
        {
            _log?.Invoke("KKS_CONVERSATION_PLAYBACK_ERROR " + exception);
            try { StopPresentation(); } catch (Exception cleanup) { _log?.Invoke("KKS_CONVERSATION_CLEANUP_ERROR " + cleanup); }
            if (Active) { Present("この会話を再生できませんでした。別の話題を選ぶか、会話を終了してください。"); ShowMenu(); }
        }
        public void End()
        {
            if (_endingVoice) { EndCore(true); return; }
            _endingVoice = true;
            if (!TryPlayBoundVoice("bye", () => EndCore(true))) EndCore(true);
        }
        public void Escape() { if (_view == null || !_view.CloseOverlay()) End(); }
        private void Present(string text) => _view.PresentLine(_characters.ConversationName, text);
        private void Invalidated() => EndCore(false);
        private void FocusChanged(bool focused)
        {
            // Keep the whole AI conversation, including preparation and connection failure UI, open
            // while the player switches to Human_2. Explicit native conversation retains its policy.
            if (!focused && !(Active && Plugin.AiConversationEnabled && !_aiNativeMenu)) EndCore(true);
        }
        private void EndCore(bool resume)
        {
            _endingVoice = false;
            if (Active) Plugin.RecordEventPositions(() => EventPositionSnapshot.Capture("conversation.end", _host,
                _player()?.transform, _characters?.ConversationTarget, _native?.Identity));
            if (Active) try { ClockSync?.Invoke(); } catch (Exception error) { _log?.Invoke("AI_LIFE_CLOCK_SAVE_ERROR " + error); }
            _placeholderVoice?.Dispose(); _placeholderVoice = null;
            var continuation = _resume; _resume = null; bool active = Active;
            if (active) KksConversationDiagnostics.KsPlugContext("end", _log);
            try { StopPresentation(); } catch (Exception exception) { _log?.Invoke("KKS_CONVERSATION_CLEANUP_ERROR " + exception); }
            _native = null; _selector = null;
            _view?.Hide();
            try { if (resume) continuation?.Invoke(); }
            catch (Exception ex) { _log?.Invoke("AI_GIRL_CONVERSATION_RESUME_ERROR " + ex); }
            finally { if (Resident?.Conversing == true) Resident.EndConversation(resume); if (active) { _changed?.Invoke(); _log?.Invoke("AI_GIRL_CONVERSATION_END resumed=" + resume); } }
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            Plugin.AiConversationSettingChanged -= AiConversationSettingChanged;
            RequestH = null;
            PlacePickup = null; PickupStatus = null;
            Application.focusChanged -= FocusChanged;
            if (_characters != null) _characters.ConversationInvalidated -= Invalidated;
            if (_characters != null) _characters.ActorChanged -= BindActor;
            EndCore(false);
            Resident?.Connections.Dispose();
            ResidentBound?.Invoke(null); ResidentBound = null; Resident = null;
            try { _voices?.Dispose(); } catch (Exception exception) { _log?.Invoke("KKS_CONVERSATION_VOICE_CLEANUP_ERROR " + exception); }
            _voices = null; _voiceBundles.Reset(); _catalog?.Clear();
            try { _view?.Dispose(); } catch (Exception exception) { _log?.Invoke("KKS_CONVERSATION_UI_CLEANUP_ERROR " + exception); }
            _view = null;
        }
    }
}
