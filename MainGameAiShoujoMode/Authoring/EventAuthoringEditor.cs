using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using CanonGate;
using MainGameAiShoujoMode.Conversation;
using MainGameUiInputCapture;
using MapEnvironmentRuntime;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MainGameAiShoujoMode.Authoring
{
    [DataContract] internal sealed class AuthoringUiSettings
    {
#pragma warning disable CS0649 // Assigned by settings.json.
        [DataMember] public float x, y, width, height, launcherX, launcherY, launcherWidth, launcherHeight, listWidth, rowHeight, buttonWidth, margin, coordinateRange, cameraDragSpeed;
        [DataMember] public int fontSize, pageSize;
#pragma warning restore CS0649
    }
    internal sealed partial class EventAuthoringEditor : MonoBehaviour, AuthoringRunner.IHost
    {
        private const string InputOwner = Plugin.Guid + ".event-authoring";
        private GateApi.ActivityRegistration activity;
        private GateApi.ActivityPermit permit;
        private MonoBehaviour modeOwner;
        private Func<bool> available;
        private AuthoringStore store;
        private AuthoringDraft draft;
        private AuthoringProject playing;
        private AuthoringStage stage;
        private AuthoringFreeHBridge freeHBridge;
        private string pendingNode;
        private Parameters runtimeSettings;
        private AuthoringProject afterHProject;
        private string afterHEvent;
        private bool afterHPreview;
        private AiCardPicker cardPicker;
        private Action<string> cardPicked;
        private AuthoringRunner runner;
        private AuthoringUiSettings cfg;
        private string content, characterId, eventId, nodeId, notice = "", importPath = "", exportPath = "", voiceSearch = "";
        private readonly Queue<Action> actions = new Queue<Action>();
        private readonly List<StageMap> maps = new List<StageMap>();
        private readonly List<VoiceItem> voices = new List<VoiceItem>();
        private sealed class VoiceItem { internal string Text, Group; internal VoiceReference Voice; }
        private Vector2 characterScroll, eventScroll, nodeScroll, detailScroll, pickerScroll;
        private Rect rect;
        private bool _visible, showingCharacters, stopped, dragging, ownsInput, focused = true, cameraOnly;
        internal bool BlocksMomentSave => visible || runner?.Active == true || stage?.Active == true;
        private bool showJson;
        private readonly HashSet<string> sections = new HashSet<string> { "scene" };
        private bool visible
        {
            get => _visible;
            set { if (_visible == value) return; _visible = value; KksConversationView.SetAuthoringControlsVisible(value); view?.SetInteractionEnabled(!value); stage?.SetDialogueInput(value || runner?.Active == true && (runner.Node?.type == "dialogue" || runner.Node?.type == "choice")); }
        }
        private int voicePage;
        private string pickerDirectory = "", pickerFilter;
        private string[] pickerFiles = Array.Empty<string>(), pickerFolders = Array.Empty<string>();
        private KeyValuePair<string, string>[] referenceChoices;
        private Action<string> referencePicked;
        private string referenceSearch = "", referenceSelected;
        private int referencePage;
        private Action<string> picked;
        private AuthoringEvent Item => draft?.Project.events.events.FirstOrDefault(e => e.id == eventId);
        private EventNode Node => Item?.nodes.FirstOrDefault(n => n.id == nodeId);
        private CharacterPreset Character => draft?.Project.characters.characters.FirstOrDefault(c => c.id == characterId);
        private KksConversationView view;
        private EventSystem events;
        private string shownNode;
        private Action nativeRestore;
        internal bool Occupied => visible || runner?.Active == true || cameraOnly;
        internal bool Running => runner?.Active == true;
        internal static EventAuthoringEditor Create(MonoBehaviour owner, string contentDirectory, Func<bool> available,
            Func<Camera> camera, Func<string, ChaControl> actor, Func<Action> suspend, Action<bool> block, IEnumerable<StageMap> knownMaps)
        {
            var editor = owner.gameObject.AddComponent<EventAuthoringEditor>();
            editor.modeOwner = owner; editor.content = contentDirectory; editor.available = available;
            editor.maps.AddRange(knownMaps);
            editor.cfg = AuthoringJson.Read<AuthoringUiSettings>(Resource("settings.json"));
            editor.rect = new Rect(editor.cfg.x, editor.cfg.y, editor.cfg.width, editor.cfg.height);
            editor.store = new AuthoringStore(Path.Combine(BepInEx.Paths.GameRootPath, "UserData", "canon_plugins", "EventAuthoring"));
            editor.stage = new AuthoringStage(editor, camera, actor, suspend, block, editor.store, editor.maps);
            editor.runner = new AuthoringRunner(editor);
            editor.runner.Changed += editor.RunChanged; editor.runner.Failed += ex => { if (editor.freeHBridge?.HoldingStage == true) editor.freeHBridge.Cancel(); editor.ShowFailure(ex); };
            // A single explicit Preview activity covers the two title-owned modes. No H-session registration.
            editor.freeHBridge = new AuthoringFreeHBridge(editor, editor.FreeHReady, editor.FreeHEnded, editor.ShowFailure);
            editor.activity = GateApi.RegisterActivity(Plugin.Guid, GateApi.ActivityKind.Preview, editor.StartAllowed, editor.StopAllowed, true);
            try { editor.activity.Begin(owner); } catch (Exception ex) { editor.ShowFailure(ex); }
            return editor;
        }
        private static string Resource(string name)
        {
            using (var stream = typeof(EventAuthoringEditor).Assembly.GetManifestResourceStream("MainGameAiShoujoMode.Authoring.Data." + name))
            { if (stream == null) throw new FileNotFoundException("エディター同梱データがありません：" + name); using (var reader = new StreamReader(stream)) return reader.ReadToEnd(); }
        }
        private void StartAllowed()
        {
            stopped = false; permit = activity.Capture();
            runtimeSettings = Parameters.Load(Path.Combine(content, "parameters.json"));
            // RegisterActivity calls back after assignment during Begin, not during registration.
            if (draft == null) draft = new AuthoringDraft(store.Load());
            EnsureTemplates();
            Application.focusChanged += FocusChanged;
        }
        private void EnsureTemplates()
        {
            foreach (var file in new[] { "settings.json", "characters.schema.json", "events.schema.json", "project.schema.json", "minimal.json", "staging.json", "freeh.json", "AI_AUTHORING.md" })
            {
                var target = Path.Combine(store.DirectoryPath, "Reference", file);
                var supplied = Resource(file); if (!File.Exists(target) || File.ReadAllText(target) != supplied) AuthoringStore.Replace(target, supplied);
            }
            var settings = Path.Combine(store.DirectoryPath, "settings.json");
            if (!File.Exists(settings)) AuthoringStore.Replace(settings, Resource("settings.json"));
            cfg = AuthoringJson.Read<AuthoringUiSettings>(File.ReadAllText(settings)); ValidateSettings();
            exportPath = Path.Combine(store.DirectoryPath, "project-export.json"); importPath = exportPath;
        }
        private void ValidateSettings()
        {
            if (cfg.width <= default(int) || cfg.height <= default(int) || cfg.listWidth <= default(int) || cfg.rowHeight <= default(int) || cfg.buttonWidth <= default(int) || cfg.pageSize <= default(int) || cfg.fontSize <= default(int) || cfg.coordinateRange <= default(int) || cfg.launcherWidth <= default(int) || cfg.launcherHeight <= default(int) || cfg.cameraDragSpeed <= default(int)) throw new InvalidDataException("settings.jsonのUI寸法・件数・調整幅は正数が必要です。");
        }
        private void StopAllowed()
        {
            stopped = true; permit = default(GateApi.ActivityPermit); visible = false;
            Application.focusChanged -= FocusChanged;
            if (freeHBridge?.HoldingStage == true) runner?.Stop(); else { StopPlayback(); stage?.Dispose(); }
            ReleaseInput();
            view?.Dispose(); view = null;
            cardPicker?.Dispose(); cardPicker = null; cardPicked = null;
            if (events != null) Destroy(events.gameObject); events = null;
            actions.Clear();
        }
        private void FocusChanged(bool value) { focused = value; if (!value) { dragging = false; ReleaseInput(); if (runner.Active) runner.SetPaused(true); } }
        private void ShowFailure(Exception ex) { notice = ex.GetBaseException().Message; }
        private void Enqueue(Action action) { actions.Enqueue(action); }
        private bool Button(string text) => GUILayout.Button(text, GUILayout.Width(cfg.buttonWidth), GUILayout.Height(cfg.rowHeight));
        private void OnGUI()
        {
            if (cfg == null || activity == null || !Plugin.ModeAllowed || !available()) { ReleaseInput(); return; }
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.RightAlt)
            {
                Enqueue(() =>
                {
                    visible = !visible;
                    if (visible && runner.Active)
                    {
                        runner.SetPaused(true); eventId = runner.EventId; nodeId = runner.Node?.id; showingCharacters = showJson = false; sections.Remove("scene"); detailScroll = Vector2.zero;
                    }
                });
                e.Use();
            }
            if (e.type == EventType.Layout)
            {
                while (actions.Count != default(int)) { var action = actions.Dequeue(); try { action(); } catch (Exception ex) { ShowFailure(ex); } }
            }
            GUI.depth = -AuthoringDefaults.Current.guiDepth;
            if (!activity.IsActive || stopped)
            {
                if (visible) GUI.Box(new Rect(cfg.x, cfg.y, Mathf.Min(cfg.width, Screen.width), cfg.rowHeight * AuthoringDefaults.Current.three), "イベント編集：canon_gate.cfg の Exceptions / ActivityAllowedPluginGuids に " + Plugin.Guid + " を追加し、モードへ入り直してください。\n右Altで閉じます。\n" + notice);
                return;
            }
            if (!permit.IsValid) { ReleaseInput(); return; }
            if (visible && focused)
            {
                rect.width = Mathf.Min(rect.width, Screen.width); rect.height = Mathf.Min(rect.height, Screen.height - cfg.margin);
                rect.x = Mathf.Clamp(rect.x, default(int), Screen.width - rect.width); rect.y = Mathf.Clamp(rect.y, default(int), Screen.height - rect.height);
                var previous = GUI.skin; var skin = GUI.skin; int label = skin.label.fontSize, text = skin.textField.fontSize, area = skin.textArea.fontSize, button = skin.button.fontSize;
                skin.label.fontSize = skin.textField.fontSize = skin.textArea.fontSize = skin.button.fontSize = cfg.fontSize;
                try { rect = GUI.Window(GetInstanceID(), rect, DrawWindow, "会話・イベント編集" + (draft.Dirty ? " ●未保存" : "")); }
                finally { skin.label.fontSize = label; skin.textField.fontSize = text; skin.textArea.fontSize = area; skin.button.fontSize = button; GUI.skin = previous; }
            }
            var capture = new List<Rect>();
            if (visible) capture.Add(rect);
            if (runner.Active && view?.Active == true) capture.Add(new Rect(default(int), default(int), Screen.width, Screen.height));
            if (capture.Count == default(int)) { ReleaseInput(); return; }
            if (focused)
            {
                UiInputCaptureApi.SetInputBlockRects(InputOwner, capture.ToArray());
                UiInputCaptureApi.SetIdleCursorUnlock(InputOwner, visible || runner.Active);
                ownsInput = true;
                if (e.rawType == EventType.MouseDown && capture.Any(r => r.Contains(e.mousePosition))) dragging = true;
                if (e.rawType == EventType.MouseUp) dragging = false;
                UiInputCaptureApi.Sync(InputOwner, "drag", dragging);
                UiInputCaptureApi.Sync(InputOwner, "text", visible && GUIUtility.keyboardControl != default(int));
            }
            if (visible && rect.Contains(e.mousePosition) && (e.isMouse || e.type == EventType.ScrollWheel)) e.Use();
        }
        private void ReleaseInput()
        {
            if (!ownsInput) return; ownsInput = false; dragging = false;
            UiInputCaptureApi.ClearInputBlockRects(InputOwner); UiInputCaptureApi.SetIdleCursorUnlock(InputOwner, false); UiInputCaptureApi.Sync(InputOwner, "drag", false);
            UiInputCaptureApi.Sync(InputOwner, "text", false);
        }
        private void DrawWindow(int id)
        {
            numericIndex = default(int);
            GUILayout.BeginHorizontal();
            if (Button("保存")) Enqueue(() => { store.Save(draft.Project); draft.MarkSaved(); notice = "保存しました。"; });
            if (Button("取り消し")) Enqueue(() => draft.Undo());
            if (Button("やり直し")) Enqueue(() => draft.Redo());
            if (Button("非表示")) Enqueue(() => visible = false);
            if (Button("閉じる・未保存取消")) Enqueue(() => { StopPlayback(); draft.Discard(); nativeRestore?.Invoke(); nativeRestore = null; visible = false; });
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (Button("① キャラを用意")) Enqueue(() => { showingCharacters = true; showJson = false; });
            if (Button("② 場面・会話を作る")) Enqueue(() => { showingCharacters = false; showJson = false; });
            if (Button("AIとJSONで編集")) Enqueue(() => showJson = !showJson);
            GUILayout.EndHorizontal();
            GUILayout.Label(notice);
            if (picked != null) { DrawFiles(); GUI.DragWindow(new Rect(default(int), default(int), rect.width, cfg.rowHeight)); return; }
            if (referencePicked != null) { DrawReferences(); GUI.DragWindow(new Rect(default(int), default(int), rect.width, cfg.rowHeight)); return; }
            if (cardPicked != null)
            {
                GUILayout.Label("使うキャラカードを画像で選んでください。男女とも選択できます。");
                if (Button("カード選択を閉じる")) Enqueue(() => cardPicked = null);
                cardPicker.Draw(Character?.card, path => Enqueue(() => { var callback = cardPicked; cardPicked = null; callback(path); }));
                GUI.DragWindow(new Rect(default(int), default(int), rect.width, cfg.rowHeight)); return;
            }
            if (showJson)
            {
                GUILayout.Label("AIには書き出したJSONとReference内の仕様・例を渡してください。読込後は下書きで確認でき、保存するまで正本を変更しません。");
                GUILayout.Label("読み込むJSON"); importPath = GUILayout.TextField(importPath);
                GUILayout.BeginHorizontal(); if (Button("ファイルを選ぶ")) Enqueue(() => OpenFiles(importPath, ".json", p => importPath = p)); if (Button("JSONを下書きへ読込")) Enqueue(Import); GUILayout.EndHorizontal();
                GUILayout.Label("書き出すJSON"); exportPath = GUILayout.TextField(exportPath);
                if (Button("現在の内容を書出し")) Enqueue(Export);
                if (Button("保存済みを再読込")) Enqueue(() => { var next = store.Load(); StopPlayback(); draft.Reset(next); cfg = AuthoringJson.Read<AuthoringUiSettings>(File.ReadAllText(Path.Combine(store.DirectoryPath, "settings.json"))); ValidateSettings(); notice = "保存済みの内容を読み込みました。"; });
                GUILayout.Label("保存先：" + store.DirectoryPath);
                GUI.DragWindow(new Rect(default(int), default(int), rect.width, cfg.rowHeight)); return;
            }
            GUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
            if (showingCharacters) DrawCharacters(); else DrawEvents();
            GUILayout.EndHorizontal();
            GUI.DragWindow(new Rect(default(int), default(int), rect.width, cfg.rowHeight));
        }
        private void Import()
        {
            var path = store.Resolve(importPath); var json = File.ReadAllText(path);
            var next = Path.GetFileName(path).Equals("characters.json", StringComparison.OrdinalIgnoreCase) ? new AuthoringProject { characters = AuthoringJson.Read<CharacterDocument>(json), events = AuthoringJson.Copy(draft.Project.events) }
                : Path.GetFileName(path).Equals("events.json", StringComparison.OrdinalIgnoreCase) ? new AuthoringProject { characters = AuthoringJson.Copy(draft.Project.characters), events = AuthoringJson.Read<EventDocument>(json) } : AuthoringJson.Read<AuthoringProject>(json);
            next.Validate(); StopPlayback(); draft.Change(p => { p.characters = next.characters; p.events = next.events; }); notice = "JSONを下書きへ読み込みました。保存で確定します。";
        }
        private void Export() { draft.Project.Validate(); AuthoringStore.Replace(store.Resolve(exportPath), AuthoringJson.Write(draft.Project)); notice = "JSONを書き出しました。"; }
        private void Field(string label, string value, Action<string> assign, bool multiline = false)
        {
            GUILayout.Label(label); string next = multiline ? GUILayout.TextArea(value ?? "", GUILayout.MinHeight(cfg.rowHeight * AuthoringDefaults.Current.three)) : GUILayout.TextField(value ?? "", GUILayout.Height(cfg.rowHeight));
            if (next != (value ?? "")) Enqueue(() => draft.Change(p => assign(next)));
        }
        private void DrawCharacters()
        {
            GUILayout.BeginVertical(GUILayout.Width(cfg.listWidth));
            if (Button("＋人物")) Enqueue(() => draft.Change(p => { var c = new CharacterPreset(); p.characters.characters.Add(c); characterId = c.id; }));
            characterScroll = GUILayout.BeginScrollView(characterScroll);
            foreach (var c in draft.Project.characters.characters) if (GUILayout.Button((c.id == characterId ? "● " : "") + c.name, GUILayout.Height(cfg.rowHeight))) { var id = c.id; Enqueue(() => characterId = id); }
            GUILayout.EndScrollView(); GUILayout.EndVertical();
            detailScroll = GUILayout.BeginScrollView(detailScroll);
            var item = Character;
            if (item != null)
            {
                GUILayout.Label("人物ID：" + item.id); Field("名前", item.name, v => item.name = v);
                Select("登場人物の割当て", item.binding, new[] { "current", "player", "reception", "card" }, new[] { "現在の女の子", "プレイヤー", "受付嬢", "カードから登場" }, v => item.binding = v);
                if (item.binding == "card")
                {
                    Field("キャラカードのパス", item.card, v => item.card = v);
                    if (Button("カード画像から選ぶ")) Enqueue(() => { cardPicker?.Dispose(); cardPicker = new AiCardPicker(runtimeSettings, Path.Combine(BepInEx.Paths.GameRootPath, "UserData", "chara"), ShowFailure); cardPicked = path => draft.Change(p => item.card = path); });
                    if (Button("パスから選ぶ")) Enqueue(() => OpenFiles(item.card, ".png", path => draft.Change(p => item.card = path)));
                }
                DrawVoice(item.voice, v => item.voice = v, item.id, false);
                GUILayout.BeginHorizontal();
                if (Button("複製")) Enqueue(() => draft.Change(p => { var c = AuthoringJson.Copy(item); c.id = Numbers.Id(); c.name += " コピー"; p.characters.characters.Add(c); characterId = c.id; }));
                if (Button("削除")) Enqueue(() => draft.Change(p => { if (p.events.events.Any(e => e.placements.Any(a => a.characterId == item.id) || e.nodes.Any(n => n.characterId == item.id || n.placements.Any(a => a.characterId == item.id)))) throw new InvalidOperationException("会話・配置で使用中です。参照を変更してから削除してください。"); p.characters.characters.Remove(item); characterId = null; }));
                GUILayout.EndHorizontal();
            }
            else GUILayout.Label("人物を選ぶか追加してください。");
            GUILayout.EndScrollView();
        }
        private void DrawEvents()
        {
            GUILayout.BeginVertical(GUILayout.Width(cfg.listWidth));
            if (Button("＋イベント")) Enqueue(() => draft.Change(p => { var e = new AuthoringEvent(); e.nodes.Add(new EventNode { text = "ここに会話を入力" }); p.events.events.Add(e); eventId = e.id; nodeId = e.nodes[default(int)].id; }));
            eventScroll = GUILayout.BeginScrollView(eventScroll);
            foreach (var item in draft.Project.events.events) if (GUILayout.Button((item.id == eventId ? "● " : "") + item.title, GUILayout.Height(cfg.rowHeight))) { var id = item.id; Enqueue(() => { eventId = id; nodeId = Item.nodes.FirstOrDefault()?.id; }); }
            GUILayout.EndScrollView();
            if (Item != null)
            {
                if (Button("イベント複製")) Enqueue(DuplicateEvent);
                if (Button("イベント削除")) Enqueue(() => draft.Change(p => { var id = Item.id; if (p.events.events.Any(e => e.id != id && (e.afterEvent == id || e.nextEvent == id || e.nodes.Any(n => n.choices.Any(c => c.targetEvent == id))))) throw new InvalidOperationException("他のイベントから参照されています。"); p.events.events.Remove(Item); eventId = nodeId = null; }));
            }
            GUILayout.EndVertical();
            GUILayout.BeginVertical(GUILayout.Width(cfg.listWidth));
            GUILayout.Label("会話の流れ");
            if (Item != null)
            {
                if (Button("＋会話／指示")) Enqueue(() => draft.Change(p => { var n = new EventNode(); Item.nodes.Add(n); nodeId = n.id; }));
                nodeScroll = GUILayout.BeginScrollView(nodeScroll);
                foreach (var node in Item.nodes) if (GUILayout.Button((node.id == nodeId ? "● " : "") + node.type + " " + (node.text ?? ""), GUILayout.MinHeight(cfg.rowHeight))) { var id = node.id; Enqueue(() => nodeId = id); }
                GUILayout.EndScrollView();
                if (Node != null)
                {
                    GUILayout.BeginHorizontal(); if (Button("↑")) Enqueue(() => MoveNode(-Numbers.One)); if (Button("↓")) Enqueue(() => MoveNode(Numbers.One)); GUILayout.EndHorizontal();
                    if (Button("会話／指示を複製")) Enqueue(() => draft.Change(p => { var n = AuthoringJson.Copy(Node); n.id = Numbers.Id(); foreach (var c in n.choices) c.id = Numbers.Id(); Item.nodes.Insert(Item.nodes.IndexOf(Node) + Numbers.One, n); nodeId = n.id; }));
                    if (Button("会話／指示を削除")) Enqueue(DeleteNode);
                }
            }
            GUILayout.EndVertical();
            detailScroll = GUILayout.BeginScrollView(detailScroll);
            if (Item != null) { DrawEventDetails(); if (Node != null) DrawNodeDetails(); }
            else GUILayout.Label("イベントを選ぶか追加してください。");
            GUILayout.EndScrollView();
        }
        private void MoveNode(int delta) { draft.Change(p => { var item = Node; int index = Item.nodes.IndexOf(item), next = index + delta; if (next < default(int) || next >= Item.nodes.Count) return; Item.nodes.RemoveAt(index); Item.nodes.Insert(next, item); }); }
        private void DeleteNode()
        {
            draft.Change(p => { var n = Node; if (Item.startNode == n.id || Item.nodes.Any(x => x != n && (x.next == n.id || x.choices.Any(c => c.targetNode == n.id)))) throw new InvalidOperationException("開始地点・分岐で参照中です。接続先を変更してから削除してください。"); Item.nodes.Remove(n); nodeId = Item.nodes.FirstOrDefault()?.id; });
        }
        private void DuplicateEvent()
        {
            draft.Change(p => { var e = AuthoringJson.Copy(Item); e.id = Numbers.Id(); e.title += " コピー"; e.afterEvent = ""; var ids = e.nodes.ToDictionary(n => n.id, n => Numbers.Id()); foreach (var n in e.nodes) { n.id = ids[n.id]; if (!string.IsNullOrEmpty(n.next)) n.next = ids[n.next]; foreach (var c in n.choices) { c.id = Numbers.Id(); if (!string.IsNullOrEmpty(c.targetNode)) c.targetNode = ids[c.targetNode]; } } if (!string.IsNullOrEmpty(e.startNode)) e.startNode = ids[e.startNode]; p.events.events.Add(e); eventId = e.id; nodeId = e.nodes.FirstOrDefault()?.id; });
        }
        private void Select(string label, string value, string[] ids, string[] names, Action<string> assign)
        {
            GUILayout.Label(label);
            int current = Array.IndexOf(ids, value ?? ""), next = GUILayout.SelectionGrid(current, names, AuthoringDefaults.Current.two);
            if (next >= default(int) && next != current) { string selected = ids[next]; Enqueue(() => draft.Change(p => assign(selected))); }
        }
        private void Reference(string label, string value, IEnumerable<KeyValuePair<string, string>> options, Action<string> assign, string empty = "指定なし")
        {
            var entries = new[] { new KeyValuePair<string, string>("", empty) }.Concat(options).ToArray();
            var selected = entries.FirstOrDefault(p => p.Key == (value ?? ""));
            GUILayout.Label(label + "：" + (selected.Value ?? ("参照先不明 " + value)));
            if (Button("選択…")) Enqueue(() => { referenceChoices = entries; referencePicked = id => draft.Change(p => assign(id)); referenceSearch = ""; referenceSelected = value ?? ""; referencePage = default(int); });
        }
        private void DrawReferences()
        {
            GUILayout.Label("候補を選択（ホイールはスクロール）");
            var search = GUILayout.TextField(referenceSearch);
            if (search != referenceSearch) Enqueue(() => { referenceSearch = search; referencePage = default(int); });
            var choices = referenceChoices.Where(p => (p.Value + " " + p.Key).IndexOf(referenceSearch, StringComparison.OrdinalIgnoreCase) >= default(int)).ToArray();
            pickerScroll = GUILayout.BeginScrollView(pickerScroll);
            foreach (var choice in choices.Skip(referencePage * cfg.pageSize).Take(cfg.pageSize)) if (GUILayout.Button((choice.Key == referenceSelected ? "● " : "") + choice.Value, GUILayout.MinHeight(cfg.rowHeight)))
            { var id = choice.Key; Enqueue(() => { var callback = referencePicked; referencePicked = null; callback(id); }); }
            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            if (Button("前のページ")) Enqueue(() => referencePage = Math.Max(default(int), referencePage - Numbers.One));
            if (Button("次のページ")) Enqueue(() => referencePage = Math.Min(Math.Max(default(int), (choices.Length - Numbers.One) / cfg.pageSize), referencePage + Numbers.One));
            if (Button("戻る")) Enqueue(() => referencePicked = null);
            GUILayout.EndHorizontal();
        }
        private IEnumerable<KeyValuePair<string, string>> Characters => draft.Project.characters.characters.Select(c => new KeyValuePair<string, string>(c.id, c.name));
        private IEnumerable<KeyValuePair<string, string>> Nodes => Item.nodes.Select((n, i) => new KeyValuePair<string, string>(n.id, (i + Numbers.One) + ". " + n.type + " " + n.text));
        private IEnumerable<KeyValuePair<string, string>> Events => draft.Project.events.events.Where(e => e.id != eventId).Select(e => new KeyValuePair<string, string>(e.id, e.title));
        private void DrawEventDetails()
        {
            var item = Item;
            GUILayout.Label("イベントID：" + item.id); Field("イベント名", item.title, v => item.title = v);
            bool sceneOpen = sections.Contains("scene");
            if (GUILayout.Button((sceneOpen ? "▼ " : "▶ ") + "場所・登場人物・アイテム・開始カメラ", GUILayout.Height(cfg.rowHeight))) Enqueue(() => { if (sceneOpen) sections.Remove("scene"); else sections.Add("scene"); });
            if (sceneOpen)
            {
            Reference("開始する会話", item.startNode, Nodes, v => item.startNode = v, "一覧の先頭");
            Reference("このイベントの完了後に開始", item.afterEvent, Events, v => item.afterEvent = v, "手動開始");
            Reference("終了後のイベント", item.nextEvent, Events, v => item.nextEvent = v);
            Select("このイベントの場面", item.sceneMode ?? "walking", new[] { "walking", "freeH" }, new[] { "歩行モードで行く", "フリーHで行く" }, v => { item.sceneMode = v; if (v == "freeH" && item.freeH == null) item.freeH = new FreeHSetup(); });
            if (item.sceneMode == "freeH")
            {
                var h = item.freeH;
                Reference("フリーHの女キャラ", h.femaleId, Characters, v => h.femaleId = v, "女キャラを選択");
                Reference("フリーHの男キャラ", h.maleId, Characters, v => h.maleId = v, "ゲームの主人公");
                PointFields("フリーHの開始位置", h.position); PointFields("フリーHの開始角度", h.rotation);
                if (Button("女キャラの位置を使う")) Enqueue(() => { var actor = stage.CurrentActor(draft.Project.characters.characters.Single(c => c.id == h.femaleId)); draft.Change(p => { h.position = AuthoringStage.Point(actor.transform.position); h.rotation = AuthoringStage.Point(actor.transform.eulerAngles); }); });
            }
            DrawMap(item.mapId, v => item.mapId = v);
            DrawPlacements(item.placements);
            DrawItems(item);
            DrawCamera(item.camera, v => item.camera = v, "開始カメラ");
            }
            GUILayout.BeginHorizontal();
            if (Button("最初から試す")) Enqueue(() => Play(null, true));
            if (Button("選択会話から試す")) Enqueue(() => Play(nodeId, true));
            if (Button("イベント実行")) Enqueue(() => Play(null, false));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (Button(runner.Paused ? "再開・編集を反映" : "一時停止")) Enqueue(() =>
            {
                if (runner.Paused)
                {
                    if (AuthoringJson.Write(playing) != AuthoringJson.Write(draft.Project))
                    {
                        draft.Project.Validate(); playing = AuthoringJson.Copy(draft.Project);
                        if (freeHBridge.Ready && freeHBridge.RequiresRestart(playing.events.events.Single(e => e.id == runner.EventId)))
                        { afterHProject = playing; afterHEvent = runner.EventId; afterHPreview = runner.Preview; freeHBridge.Cancel(); return; }
                        stage.RefreshBindings(playing); if (freeHBridge.Ready) freeHBridge.UpdateProject(playing); runner.ApplyDraft(playing);
                    }
                    runner.SetPaused(false); visible = false;
                }
                else runner.SetPaused(true);
            });
            if (Button("停止・元へ戻る")) Enqueue(StopPlayback);
            if (Button("会話を表示／非表示")) Enqueue(() => { if (view != null) view.SetDisplayHidden(!view.DisplayHidden); });
            GUILayout.EndHorizontal();
        }
        private void DrawNodeDetails()
        {
            var n = Node;
            GUILayout.Box("選択中の会話／指示：" + n.id);
            Select("種類", n.type, new[] { "dialogue", "choice", "wait", "camera", "map", "placement", "end" }, new[] { "会話", "選択肢", "待ち時間", "カメラ", "マップ変更", "人物配置", "終了" }, v => n.type = v);
            if (n.type == "dialogue" || n.type == "choice")
            {
                Reference("話者", n.characterId, Characters, v => n.characterId = v, "地の文"); Field("本文", n.text, v => n.text = v, true);
                Number("目の表情番号（-1：変更なし）", n.expression, v => n.expression = (int)v, -Numbers.One, AuthoringDefaults.Current.maxExpression);
                DrawVoice(n.voice, v => n.voice = v, n.characterId, true);
                DrawAnimations(n);
                if (n.type == "choice")
                {
                    foreach (var c in n.choices)
                    {
                        Field("選択肢", c.text, v => c.text = v);
                        Reference("選択後の会話", c.targetNode, Nodes, v => { c.targetNode = v; if (!string.IsNullOrEmpty(v)) c.targetEvent = ""; }, "次の会話");
                        Reference("選択後のイベント", c.targetEvent, Events, v => { c.targetEvent = v; if (!string.IsNullOrEmpty(v)) c.targetNode = ""; });
                        if (Button("選択肢を削除")) Enqueue(() => draft.Change(p => n.choices.Remove(c)));
                    }
                    if (Button("＋選択肢")) Enqueue(() => draft.Change(p => n.choices.Add(new EventChoice())));
                }
            }
            if (n.type == "wait") Number("待ち時間（秒）", n.seconds, v => n.seconds = v, default(int), AuthoringDefaults.Current.maxWaitSeconds);
            if (n.type == "map") DrawMap(n.mapId, v => n.mapId = v);
            if (n.type == "map" || n.type == "placement") DrawPlacements(n.placements);
            if (n.type == "camera" || n.type == "map") DrawCamera(n.camera, v => n.camera = v, "カメラ");
            Reference("次の会話／指示", n.next, Nodes.Where(p => p.Key != n.id), v => n.next = v, "一覧の次（末尾は終了）");
        }
        private void DrawMap(string id, Action<string> assign)
        {
            Reference("マップ", id == "current" ? "" : id, maps.Select(m => new KeyValuePair<string, string>(m.Id, m.Name + (string.IsNullOrEmpty(m.Error) ? "" : "［" + m.Error + "］"))), v => assign(string.IsNullOrEmpty(v) ? "current" : v), "現在のマップ");
            if (Button("このマップを見る")) Enqueue(() =>
            {
                StopPlayback(); stage.Begin(new AuthoringProject()); cameraOnly = true;
                stage.Map(id, () => { stage.ConfigureWalking(runtimeSettings); notice = "マップを表示しました。実物から家具・小物を選べます。「停止」で元の場面へ戻ります。"; }, ShowFailure);
            });
        }
        private void DrawPlacements(List<ActorPlacement> placements)
        {
            GUILayout.Label("人物配置（ワールド座標）");
            foreach (var p in placements)
            {
                Reference("人物", p.characterId, Characters, v => p.characterId = v);
                PointFields("位置", p.position); PointFields("向き（度）", p.rotation);
                if (p.condition == null) { if (Button("人物の状態を指定")) Enqueue(() => draft.Change(project => p.condition = new ActorCondition())); }
                else
                {
                    bool show = GUILayout.Toggle(p.condition.visible, "この人物を表示する"); if (show != p.condition.visible) Enqueue(() => draft.Change(project => p.condition.visible = show));
                    Select("服装の状態", p.condition.clothing, new[] { "preserve", "full", "half", "nude" }, new[] { "現在の服装", "着衣", "半脱ぎ", "脱衣" }, v => p.condition.clothing = v);
                }
                if (p.animation == null) { if (Button("最初の姿勢を指定")) Enqueue(() => draft.Change(project => p.animation = new MotionCue { characterId = p.characterId })); }
                else { DrawMotion(p.animation); if (Button("最初のアニメを外す")) Enqueue(() => draft.Change(project => p.animation = null)); }
                if (Button("現在の人物位置")) Enqueue(() => { var actor = stage.CurrentActor(draft.Project.characters.characters.Single(c => c.id == p.characterId)); var t = actor.transform.parent ?? actor.transform; draft.Change(project => { p.position = AuthoringStage.Point(t.position); p.rotation = AuthoringStage.Point(t.eulerAngles); }); });
                if (Button("配置を削除")) Enqueue(() => draft.Change(project => placements.Remove(p)));
            }
            if (Button("＋人物配置")) Enqueue(() => draft.Change(p => placements.Add(new ActorPlacement { characterId = p.characters.characters.FirstOrDefault()?.id ?? "" })));
        }
        private void PointFields(string label, Point point)
        { Number(label + " X", point.x, v => point.x = v, point.x - cfg.coordinateRange, point.x + cfg.coordinateRange); Number(label + " Y", point.y, v => point.y = v, point.y - cfg.coordinateRange, point.y + cfg.coordinateRange); Number(label + " Z", point.z, v => point.z = v, point.z - cfg.coordinateRange, point.z + cfg.coordinateRange); }
        private readonly Dictionary<string, string> numeric = new Dictionary<string, string>();
        private readonly Dictionary<string, Vector2> numericRanges = new Dictionary<string, Vector2>();
        private int numericIndex;
        private void Number(string label, float value, Action<float> assign, float min, float max)
        {
            var key = characterId + "/" + eventId + "/" + nodeId + "/" + label + "/" + numericIndex++;
            GUILayout.Label(label); GUILayout.BeginHorizontal();
            if (!numericRanges.TryGetValue(key, out var range) || GUIUtility.hotControl == default(int)) numericRanges[key] = range = new Vector2(min, max);
            float next = GUILayout.HorizontalSlider(value, range.x, range.y);
            string raw = numeric.TryGetValue(key, out var input) ? input : value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var edited = GUILayout.TextField(raw, GUILayout.Width(cfg.buttonWidth));
            if (edited != raw) { numeric[key] = edited; if (float.TryParse(edited, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed) && Numbers.Finite(parsed)) next = parsed; }
            if (next != value) { float result = next; numeric[key] = result.ToString(System.Globalization.CultureInfo.InvariantCulture); Enqueue(() => draft.Change(p => { assign(result); PreviewDraft(); })); }
            GUILayout.EndHorizontal();
        }
        private void PreviewDraft()
        {
            if (!runner.Active && !cameraOnly) return;
            var cue = Node?.camera ?? Item?.camera;
            if (cue != null) { var immediate = AuthoringJson.Copy(cue); immediate.seconds = default(int); stage.CameraTo(immediate, () => { }); }
            if (Node?.placements != null) stage.Place(Node.placements);
            if (Item?.placements != null) stage.Place(Item.placements);
        }
        private void DrawCamera(CameraCue cue, Action<CameraCue> assign, string label)
        {
            GUILayout.Label(label);
            if (Button("現在の構図を登録")) Enqueue(() => { var c = stage.Capture(); draft.Change(p => assign(c)); });
            if (cue == null) return;
            PointFields("カメラ位置", cue.position); PointFields("カメラ角度", cue.rotation);
            Number("画角（度）", cue.fieldOfView, v => cue.fieldOfView = v, Numbers.One, (AuthoringDefaults.Current.maxFieldOfView - Numbers.One)); Number("移動時間（秒）", cue.seconds, v => cue.seconds = v, default(int), AuthoringDefaults.Current.maxCameraSeconds);
            Select("移動", cue.ease, new[] { "linear", "smooth" }, new[] { "一定速度", "滑らか" }, v => cue.ease = v);
            if (Button("この移動だけ試す")) Enqueue(() => { StopPlayback(); playing = AuthoringJson.Copy(draft.Project); stage.Begin(playing); cameraOnly = true; stage.CameraTo(cue, () => { notice = "カメラ移動の試し再生完了。「停止」で元へ戻ります。"; }); });
            if (Button("カメラ指定を削除")) Enqueue(() => draft.Change(p => assign(null)));
        }
        private void DrawVoice(VoiceReference voice, Action<VoiceReference> assign, string actor, bool inherit)
        {
            var types = inherit ? new[] { "inherit", "none", "game", "file" } : new[] { "none", "game", "file" };
            var labels = inherit ? new[] { "人物の設定", "音声なし", "ゲーム内音声", "外部ファイル" } : new[] { "音声なし", "ゲーム内音声", "外部ファイル" };
            Select("音声", voice?.source ?? "inherit", types, labels, v => assign(v == "inherit" ? null : new VoiceReference { source = v }));
            if (voice == null) return;
            if (voice.source == "game")
            {
                Field("AssetBundle", voice.bundle, v => voice.bundle = v); Field("音声資産名", voice.asset, v => voice.asset = v);
                Number("音声の性格番号", voice.personality, v => voice.personality = (int)v, default(int), AuthoringDefaults.Current.maxPersonality);
                if (Button("ゲーム音声一覧を更新")) Enqueue(RefreshVoices);
                voiceSearch = GUILayout.TextField(voiceSearch); var matches = voices.Where(v => v.Voice.personality == voice.personality && (v.Text + " " + v.Group + " " + v.Voice.asset).IndexOf(voiceSearch, StringComparison.OrdinalIgnoreCase) >= default(int)).ToArray();
                foreach (var v in matches.Skip(voicePage * cfg.pageSize).Take(cfg.pageSize)) if (GUILayout.Button(v.Group + " / " + v.Text, GUILayout.MinHeight(cfg.rowHeight))) { var selected = v; Enqueue(() => draft.Change(p => { var copy = AuthoringJson.Copy(selected.Voice); copy.volume = voice.volume; copy.pitch = voice.pitch; assign(copy); })); }
                GUILayout.BeginHorizontal(); if (Button("前の音声ページ")) Enqueue(() => voicePage = Math.Max(default(int), voicePage - Numbers.One)); if (Button("次の音声ページ")) Enqueue(() => voicePage = Math.Min(Math.Max(default(int), (matches.Length - Numbers.One) / cfg.pageSize), voicePage + Numbers.One)); GUILayout.EndHorizontal();
            }
            if (voice.source == "file") { Field("音声パス（WAV / OGG / MP3）", voice.path, v => voice.path = v); if (Button("音声ファイル選択")) Enqueue(() => OpenFiles(voice.path, "audio", path => draft.Change(p => voice.path = path))); }
            if (voice.source != "none")
            {
                Number("音量", voice.volume, v => voice.volume = v, default(int), Numbers.One); Number("ピッチ", voice.pitch, v => voice.pitch = v, AuthoringDefaults.Current.minPitch, AuthoringDefaults.Current.three);
                if (Button("試聴")) Enqueue(() => { if (!runner.Active && !cameraOnly) { stage.Begin(new AuthoringProject()); cameraOnly = true; } stage.PlayVoice(voice, null, ShowFailure); });
                if (Button("試聴停止")) Enqueue(() => { stage.StopVoice(); if (!runner.Active) StopPlayback(); });
            }
        }
        private void OpenFiles(string initial, string filter, Action<string> selected)
        {
            pickerFilter = filter; picked = selected; pickerDirectory = string.IsNullOrWhiteSpace(initial) ? store.DirectoryPath : store.Resolve(initial);
            if (!Directory.Exists(pickerDirectory)) pickerDirectory = Path.GetDirectoryName(pickerDirectory);
            if (!Directory.Exists(pickerDirectory)) pickerDirectory = store.DirectoryPath;
            RefreshFiles();
        }
        private void RefreshFiles() { pickerFolders = Directory.GetDirectories(pickerDirectory); pickerFiles = Directory.GetFiles(pickerDirectory).Where(p => pickerFilter == "audio" ? new[] { ".wav", ".ogg", ".mp3" }.Contains(Path.GetExtension(p).ToLowerInvariant()) : Path.GetExtension(p).Equals(pickerFilter, StringComparison.OrdinalIgnoreCase)).ToArray(); }
        private void DrawFiles()
        {
            GUILayout.Label("ファイル選択"); pickerDirectory = GUILayout.TextField(pickerDirectory);
            GUILayout.BeginHorizontal(); if (Button("フォルダーへ移動")) Enqueue(RefreshFiles); if (Button("上のフォルダー")) Enqueue(() => { pickerDirectory = Path.GetDirectoryName(pickerDirectory) ?? pickerDirectory; RefreshFiles(); }); if (Button("選択を閉じる")) Enqueue(() => picked = null); GUILayout.EndHorizontal();
            pickerScroll = GUILayout.BeginScrollView(pickerScroll);
            foreach (var folder in pickerFolders) if (GUILayout.Button("［フォルダー］ " + Path.GetFileName(folder))) { var path = folder; Enqueue(() => { pickerDirectory = path; RefreshFiles(); }); }
            foreach (var file in pickerFiles) if (GUILayout.Button(Path.GetFileName(file))) { var path = file; Enqueue(() => { var action = picked; picked = null; action(path); }); }
            GUILayout.EndScrollView();
        }
        private void RefreshVoices()
        {
            voices.Clear(); voicePage = default(int);
            var catalogPath = Path.Combine(content, "kks-character-voices.json");
            if (File.Exists(catalogPath))
            {
                var catalog = KksCharacterVoiceCatalog.Load(catalogPath);
                foreach (var entry in catalog.entries) voices.Add(new VoiceItem { Text = string.IsNullOrEmpty(entry.text) ? entry.asset : entry.text, Group = string.Join(" / ", entry.sources.Select(s => s.group).Distinct()), Voice = new VoiceReference { source = "game", personality = catalog.personality, bundle = entry.bundle, asset = entry.asset } });
            }
            // Asset names from installed voice bundles supplement the annotated source catalog for every personality.
            var root = Path.Combine(BepInEx.Paths.GameRootPath, "abdata", "sound", "data", "pcm");
            if (Directory.Exists(root)) foreach (var directory in Directory.GetDirectories(root))
            {
                var name = Path.GetFileName(directory); if (!name.StartsWith("c", StringComparison.Ordinal) || !int.TryParse(name.Substring(Numbers.One), out var personality)) continue;
                foreach (var file in Directory.GetFiles(directory, "*.unity3d", SearchOption.AllDirectories))
                {
                    // Enumerate headers only; clips remain unloaded until explicitly selected for playback.
                    var bundleName = "sound/data/pcm/" + name + "/" + file.Substring(directory.Length + Numbers.One).Replace('\\', '/');
                    var loaded = AssetBundle.GetAllLoadedAssetBundles().FirstOrDefault(b => b.name == bundleName);
                    var bundle = loaded ?? AssetBundle.LoadFromFile(file);
                    if (bundle == null) continue;
                    try { foreach (var asset in bundle.GetAllAssetNames()) { var assetName = Path.GetFileNameWithoutExtension(asset); if (voices.Any(v => v.Voice.bundle == bundleName && v.Voice.asset == assetName)) continue; voices.Add(new VoiceItem { Text = assetName + "（原文未確認）", Group = Path.GetFileNameWithoutExtension(file), Voice = new VoiceReference { source = "game", personality = personality, bundle = bundleName, asset = assetName } }); } }
                    finally { if (loaded == null) bundle.Unload(false); }
                }
            }
            notice = "ゲーム音声 " + voices.Count + "件。性格番号と検索欄で絞り込めます。";
        }
        private void EnsureView()
        {
            if (view == null) { view = new KksConversationView(transform, content, () => view.SetDisplayHidden(true)); view.SetInteractionEnabled(!visible); view.EditRequested = (name, text) => { if (!runner.Active || runner.Node == null) return; eventId = runner.EventId; nodeId = runner.Node.id; showingCharacters = false; showJson = false; sections.Remove("scene"); detailScroll = Vector2.zero; visible = true; runner.SetPaused(true); }; }
            if (EventSystem.current == null && events == null) { var root = new GameObject("Event authoring UI events", typeof(EventSystem), typeof(StandaloneInputModule)); root.transform.SetParent(transform, false); events = root.GetComponent<EventSystem>(); }
        }
        private void Play(string selectedNode, bool preview)
        {
            var id = eventId; draft.Project.Validate(); StopPlayback(); playing = AuthoringJson.Copy(draft.Project); shownNode = null;
            EnsureView();
            pendingNode = selectedNode; runner.Start(playing, id, selectedNode, preview); visible = false;
        }
        void AuthoringRunner.IHost.Enter(AuthoringEvent item, Action completed, Action<Exception> failed)
        {
            if (freeHBridge.Ready)
            {
                if (freeHBridge.EventId != item.id) { afterHProject = AuthoringJson.Copy(playing); afterHEvent = item.id; afterHPreview = runner.Preview; freeHBridge.Cancel(); return; }
                stage.Place(item.placements); stage.CameraTo(item.camera, completed); return;
            }
            stage.Begin(playing); stage.Map(item.mapId, () =>
            {
                if (!permit.IsValid) return;
                try
                {
                    stage.Place(item.placements); stage.PlaceItems(item.items);
                    if (item.sceneMode == "freeH") { freeHBridge.Begin(stage, playing, item, pendingNode, runner.Preview, runtimeSettings); return; }
                    stage.ConfigureWalking(runtimeSettings); stage.SetDialogueInput(true); stage.CameraTo(item.camera, completed);
                }
                catch (Exception ex) { failed(ex); }
            }, failed);
        }
        void AuthoringRunner.IHost.Execute(EventNode node, Action completed, Action<Exception> failed)
        {
            if (!permit.IsValid) throw new InvalidOperationException("編集セッションの許可が失効しました。");
            stage.CancelCue(); view?.SetChoices(Array.Empty<KksConversationView.Choice>());
            stage.SetDialogueInput(true);
            foreach (var motion in node.animations ?? Enumerable.Empty<MotionCue>())
            {
                var h = playing.events.events.Single(e => e.id == runner.EventId).freeH;
                if (freeHBridge.Ready && h != null && (motion.characterId == h.femaleId || motion.characterId == h.maleId)) throw new InvalidOperationException("フリーHの参加者のアニメは「体位一覧から選ぶ」で指定してください。背景の人物には通常のアニメを使えます。");
                stage.Perform(motion);
            }
            if (node.hAnimation != null) freeHBridge.Animate(node.hAnimation);
            if (node.type == "dialogue" || node.type == "choice")
            {
                EnsureView(); var actor = playing.characters.characters.FirstOrDefault(c => c.id == node.characterId);
                view.Show(actor?.name ?? "", node.text); shownNode = node.id; view.AdvanceRequested = () => Enqueue(() => runner.Advance());
                view.SetCanAdvance(node.type == "dialogue");
                stage.Expression(node.characterId, node.expression); stage.PlayVoice(node.voice ?? actor?.voice, node.characterId, failed);
                if (node.type == "choice") view.SetChoices(node.choices.Select(c => new KksConversationView.Choice(c.text, () => Enqueue(() => runner.Choose(c.id)))));
                return;
            }
            if (node.type == "wait") { stage.Wait(node.seconds, completed); return; }
            if (node.type == "camera") { stage.CameraTo(node.camera, completed); return; }
            if (node.type == "placement") { stage.Place(node.placements); completed(); return; }
            if (node.type == "map")
            {
                if (freeHBridge.Ready) throw new InvalidOperationException("フリーHで次のマップへ移る場合は、次のイベントのマップを指定して接続してください。現在のHを終了してから次の場面を開きます。");
                stage.Map(node.mapId, () => { if (!permit.IsValid) return; try { stage.Place(node.placements); stage.CameraTo(node.camera, completed); } catch (Exception ex) { failed(ex); } }, failed); return;
            }
            completed();
        }
        void AuthoringRunner.IHost.Pause(bool value) => stage.Pause(value);
        void AuthoringRunner.IHost.Restore() { if (freeHBridge.HoldingStage) { if (runner.Preview && freeHBridge.Ready && !stopped) freeHBridge.Cancel(); } else { if (runner.Preview) stage.Dispose(); else { cameraOnly = true; stage.SetDialogueInput(false); } } view?.Hide(); }
        private void RunChanged() { if (!runner.Active) { view?.Hide(); notice = freeHBridge?.HoldingStage == true ? "会話を終了しました。フリーHの終了・復帰を待っています。" : cameraOnly ? "会話を終了しました。この場面で歩けます。停止で元の場所へ戻ります。" : "停止しました。元の場面へ戻りました。"; } }
        private void StopPlayback() { afterHProject = null; afterHEvent = null; runner?.Stop(); if (freeHBridge?.HoldingStage == true) freeHBridge.Cancel(); else stage?.Dispose(); cameraOnly = false; view?.Hide(); playing = null; nativeRestore?.Invoke(); nativeRestore = null; }
        private void FreeHReady()
        {
            playing = AuthoringJson.Copy(freeHBridge.Project); EnsureView();
            runner.Start(playing, freeHBridge.EventId, freeHBridge.NodeId, freeHBridge.Preview); visible = false;
        }
        private void FreeHEnded()
        {
            var next = afterHProject; var id = afterHEvent; var preview = afterHPreview; afterHProject = null; afterHEvent = null;
            runner.Stop(); cameraOnly = false; view?.Hide(); activity?.End(); activity?.Begin(modeOwner);
            if (next != null)
            {
                if (!permit.IsValid) { notice = "次の場面の編集許可がありません。保存済みイベントは保持しています。"; return; }
                playing = next; pendingNode = null; runner.Start(playing, id, null, preview);
            }
        }
        internal void CaptureNative(KksConversationView native, string name, string text)
        {
            if (!permit.IsValid || draft == null) return;
            Enqueue(() => { draft.Change(p => { var character = p.characters.characters.FirstOrDefault(c => c.name == name); if (character == null && !string.IsNullOrEmpty(name)) { character = new CharacterPreset { name = name }; p.characters.characters.Add(character); } var item = new AuthoringEvent { title = "取り込んだ会話" }; var n = new EventNode { text = text, characterId = character?.id ?? "" }; item.nodes.Add(n); p.events.events.Add(item); eventId = item.id; nodeId = n.id; }); showingCharacters = false; visible = true; native.SetDisplayHidden(true); nativeRestore = () => native.SetDisplayHidden(false); notice = "現在の表示を自作イベントへ取り込みました。原本のKKS会話表は変更しません。"; });
        }
        internal void Shutdown() { freeHBridge?.Dispose(); StopAllowed(); activity?.Dispose(); activity = null; }
        internal void SuspendActivity() { activity?.End(); }
        internal void ResumeActivity() { if (activity != null) activity.Begin(modeOwner); }
        private void OnDisable() { if (activity != null) Shutdown(); }
        private void OnDestroy() { Shutdown(); }
    }
}
