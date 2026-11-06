using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

namespace MainGameAiShoujoMode.Authoring
{
#pragma warning disable CS0649 // Public JSON fields are also populated by DataContractJsonSerializer.
    [DataContract] internal sealed class VoiceReference
    {
        [DataMember] public string source = "none", bundle = "", asset = "", path = "";
        [DataMember] public int personality;
        [DataMember] public float volume = Numbers.One, pitch = Numbers.One;
        [OnDeserializing] private void Reading(StreamingContext _) { source = "none"; bundle = asset = path = ""; volume = pitch = Numbers.One; }
        internal void Validate(string location)
        {
            if (source != "none" && source != "game" && source != "file") throw new InvalidDataException(location + ": 音声sourceはnone/game/fileです。");
            if (source == "game" && (string.IsNullOrWhiteSpace(bundle) || string.IsNullOrWhiteSpace(asset))) throw new InvalidDataException(location + ": ゲーム音声のbundle/assetが必要です。");
            if (source == "file" && string.IsNullOrWhiteSpace(path)) throw new InvalidDataException(location + ": 音声pathが必要です。");
            if (!Numbers.Finite(volume) || volume < default(int) || volume > Numbers.One || !Numbers.Finite(pitch) || pitch <= default(int) || pitch > AuthoringDefaults.Current.three || personality < default(int)) throw new InvalidDataException(location + ": 音量・ピッチ・性格番号が不正です。");
        }
    }
    internal static class Numbers
    {
        internal static int One => AuthoringDefaults.Current.one;
        internal static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
        internal static string Id() => Guid.NewGuid().ToString("N");
    }
    [DataContract] internal sealed class Point
    {
        [DataMember] public float x, y, z;
        internal void Validate(string location) { if (!Numbers.Finite(x) || !Numbers.Finite(y) || !Numbers.Finite(z)) throw new InvalidDataException(location + ": 座標が不正です。"); }
    }
    [DataContract] internal sealed class CameraCue
    {
        [DataMember] public Point position = new Point(), rotation = new Point();
        [DataMember] public float fieldOfView = AuthoringDefaults.Current.defaultFieldOfView, seconds;
        [DataMember] public string ease = "smooth";
        [OnDeserializing] private void Reading(StreamingContext _) { ease = "smooth"; fieldOfView = AuthoringDefaults.Current.defaultFieldOfView; }
        internal void Validate(string location)
        {
            if (position == null || rotation == null) throw new InvalidDataException(location + ": カメラ座標がありません。");
            position.Validate(location); rotation.Validate(location);
            if (!Numbers.Finite(fieldOfView) || fieldOfView <= default(int) || fieldOfView >= AuthoringDefaults.Current.maxFieldOfView || !Numbers.Finite(seconds) || seconds < default(int) || (ease != "linear" && ease != "smooth")) throw new InvalidDataException(location + ": カメラ設定が不正です。");
        }
    }
    [DataContract] internal sealed class CharacterPreset
    {
        [DataMember] public string id = Numbers.Id(), name = "新しい人物", binding = "current", card = "";
        [DataMember] public VoiceReference voice = new VoiceReference();
        [OnDeserializing] private void Reading(StreamingContext _) { card = ""; }
    }
    [DataContract] internal sealed class ActorPlacement
    {
        [DataMember] public string characterId;
        [DataMember] public Point position = new Point(), rotation = new Point();
        [DataMember] public ActorCondition condition;
        [DataMember] public MotionCue animation;
    }
    [DataContract] internal sealed class ActorCondition
    {
        [DataMember] public bool visible = true;
        [DataMember] public string clothing = "preserve";
        [OnDeserializing] private void Reading(StreamingContext _) { visible = true; clothing = "preserve"; }
    }
    [DataContract] internal sealed class MotionCue
    {
        [DataMember] public string characterId = "", source = "clip", bundle = "", asset = "", state = "";
        [DataMember] public float speed = Numbers.One, startSeconds, blendSeconds;
        [DataMember] public bool loop = true;
        [OnDeserializing] private void Reading(StreamingContext _) { characterId = state = ""; speed = Numbers.One; loop = true; }
        internal void Validate(string location)
        {
            if ((source != "clip" && source != "controller") || string.IsNullOrWhiteSpace(bundle) || string.IsNullOrWhiteSpace(asset)
                || source == "controller" && string.IsNullOrWhiteSpace(state) || !Numbers.Finite(speed) || speed <= default(int) || !Numbers.Finite(startSeconds) || startSeconds < default(int) || !Numbers.Finite(blendSeconds) || blendSeconds < default(int)) throw new InvalidDataException(location + ": アニメの種類・素材・速度・開始秒・遷移秒を確認してください。");
        }
    }
    [DataContract] internal sealed class ItemPlacement
    {
        [DataMember] public string id = Numbers.Id(), name = "アイテム", source = "map", path = "", bundle = "", asset = "";
        [DataMember] public Point position = new Point(), rotation = new Point();
        [DataMember] public bool visible = true;
        [OnDeserializing] private void Reading(StreamingContext _) { path = bundle = asset = ""; visible = true; }
    }
    [DataContract] internal sealed class FreeHSetup
    {
        [DataMember] public string femaleId = "", maleId = "";
        [DataMember] public Point position = new Point(), rotation = new Point();
        [OnDeserializing] private void Reading(StreamingContext _) { maleId = ""; }
    }
    [DataContract] internal sealed class HAnimationCue
    {
        [DataMember] public string mode, name, bundle, asset;
        [DataMember] public int id;
    }
    [DataContract] internal sealed class EventChoice
    {
        [DataMember] public string id = Numbers.Id(), text = "選択肢", targetNode = "", targetEvent = "";
        [OnDeserializing] private void Reading(StreamingContext _) { targetNode = targetEvent = ""; }
    }
    [DataContract] internal sealed class EventNode
    {
        [DataMember] public string id = Numbers.Id(), type = "dialogue", text = "", characterId = "", next = "", mapId = "";
        [DataMember] public VoiceReference voice;
        [DataMember] public int expression = -Numbers.One;
        [DataMember] public float seconds;
        [DataMember] public CameraCue camera;
        [DataMember] public List<ActorPlacement> placements = new List<ActorPlacement>();
        [DataMember] public List<EventChoice> choices = new List<EventChoice>();
        [DataMember] public List<MotionCue> animations;
        [DataMember] public HAnimationCue hAnimation;
        [OnDeserializing] private void Reading(StreamingContext _) { text = characterId = next = mapId = ""; expression = -Numbers.One; }
    }
    [DataContract] internal sealed class AuthoringEvent
    {
        [DataMember] public string id = Numbers.Id(), title = "新しいイベント", startNode = "", mapId = "current", nextEvent = "", afterEvent = "";
        [DataMember] public CameraCue camera;
        [DataMember] public List<ActorPlacement> placements = new List<ActorPlacement>();
        [DataMember] public List<EventNode> nodes = new List<EventNode>();
        [DataMember] public string sceneMode;
        [DataMember] public FreeHSetup freeH;
        [DataMember] public List<ItemPlacement> items;
        [OnDeserializing] private void Reading(StreamingContext _) { startNode = nextEvent = afterEvent = ""; mapId = "current"; sceneMode = "walking"; }
    }
    [DataContract] internal sealed class CharacterDocument
    {
        [DataMember] public int schemaVersion = Numbers.One;
        [DataMember] public List<CharacterPreset> characters = new List<CharacterPreset>();
    }
    [DataContract] internal sealed class EventDocument
    {
        [DataMember] public int schemaVersion = Numbers.One;
        [DataMember] public List<AuthoringEvent> events = new List<AuthoringEvent>();
    }
    [DataContract] internal sealed class AuthoringProject
    {
        [DataMember] public CharacterDocument characters = new CharacterDocument();
        [DataMember] public EventDocument events = new EventDocument();
        internal void Validate()
        {
            if (characters?.schemaVersion != Numbers.One || events?.schemaVersion != Numbers.One || characters.characters == null || events.events == null) throw new InvalidDataException("schemaVersion=1、characters/events配列が必要です。");
            Unique(characters.characters.Select(c => c?.id), "characters"); Unique(events.events.Select(e => e?.id), "events");
            var ids = new HashSet<string>(characters.characters.Select(c => c.id)); var eventIds = new HashSet<string>(events.events.Select(e => e.id));
            foreach (var c in characters.characters)
            {
                if (string.IsNullOrWhiteSpace(c.name) || (c.binding != "current" && c.binding != "player" && c.binding != "reception" && c.binding != "card")) throw new InvalidDataException("characters/" + c.id + ": 名前かbindingが不正です。");
                if (c.binding == "card" && string.IsNullOrWhiteSpace(c.card)) throw new InvalidDataException("characters/" + c.id + ": カードpathが必要です。");
                if (c.voice == null) throw new InvalidDataException("characters/" + c.id + ": voiceが必要です。"); c.voice.Validate("characters/" + c.id);
            }
            foreach (var e in events.events)
            {
                var loc = "events/" + e.id;
                if (string.IsNullOrEmpty(e.sceneMode)) e.sceneMode = "walking";
                if (e.sceneMode != "walking" && e.sceneMode != "freeH") throw new InvalidDataException(loc + ": 場面はwalking/freeHです。");
                if (e.sceneMode == "freeH") { if (e.freeH == null || string.IsNullOrEmpty(e.freeH.femaleId) || e.freeH.position == null || e.freeH.rotation == null) throw new InvalidDataException(loc + ": フリーHに参加する女の子と開始位置・角度を選んでください。"); Reference(e.freeH.femaleId, ids, loc + "/freeH/femaleId"); Reference(e.freeH.maleId, ids, loc + "/freeH/maleId"); e.freeH.position.Validate(loc); e.freeH.rotation.Validate(loc); }
                if (e.items != null)
                {
                    Unique(e.items.Select(i => i?.id), loc + "/items");
                    foreach (var i in e.items) { if (string.IsNullOrWhiteSpace(i.name) || i.source != "map" && i.source != "prefab" || i.source == "map" && string.IsNullOrWhiteSpace(i.path) || i.source == "prefab" && (string.IsNullOrWhiteSpace(i.bundle) || string.IsNullOrWhiteSpace(i.asset)) || i.position == null || i.rotation == null) throw new InvalidDataException(loc + "/items/" + i.id + ": アイテムの素材と配置を確認してください。"); i.position.Validate(loc); i.rotation.Validate(loc); }
                }
                if (string.IsNullOrWhiteSpace(e.title) || e.nodes == null || e.placements == null) throw new InvalidDataException(loc + ": title/nodes/placementsが必要です。");
                Unique(e.nodes.Select(n => n?.id), loc + "/nodes"); var nodeIds = new HashSet<string>(e.nodes.Select(n => n.id));
                Reference(e.startNode, nodeIds, loc + "/startNode"); Reference(e.nextEvent, eventIds, loc + "/nextEvent"); Reference(e.afterEvent, eventIds, loc + "/afterEvent");
                if (e.afterEvent == e.id || e.nextEvent == e.id) throw new InvalidDataException(loc + ": 自分自身へのイベント接続はできません。");
                e.camera?.Validate(loc + "/camera"); ValidatePlacements(e.placements, ids, loc);
                foreach (var n in e.nodes)
                {
                    var p = loc + "/nodes/" + n.id;
                    if (!new[] { "dialogue", "choice", "wait", "camera", "map", "placement", "end" }.Contains(n.type)) throw new InvalidDataException(p + ": 未知のtypeです。");
                    Reference(n.characterId, ids, p + "/characterId"); Reference(n.next, nodeIds, p + "/next");
                    n.voice?.Validate(p + "/voice"); n.camera?.Validate(p + "/camera");
                    if (n.animations != null) foreach (var a in n.animations) { if (a == null || string.IsNullOrEmpty(a.characterId)) throw new InvalidDataException(p + ": アニメを使う人物を選んでください。"); Reference(a.characterId, ids, p + "/animations"); a.Validate(p); }
                    if (n.hAnimation != null && (string.IsNullOrEmpty(n.hAnimation.mode) || string.IsNullOrEmpty(n.hAnimation.name) || n.hAnimation.id < default(int))) throw new InvalidDataException(p + ": フリーHの体位指定が不正です。");
                    if (!Numbers.Finite(n.seconds) || n.seconds < default(int) || n.expression < -Numbers.One || n.placements == null || n.choices == null) throw new InvalidDataException(p + ": 時間・表情・配列が不正です。");
                    ValidatePlacements(n.placements, ids, p);
                    if (n.type == "map" && string.IsNullOrWhiteSpace(n.mapId) || n.type == "camera" && n.camera == null || n.type == "choice" && n.choices.Count == default(int)) throw new InvalidDataException(p + ": typeに必要な設定がありません。");
                    Unique(n.choices.Select(c => c?.id), p + "/choices");
                    foreach (var c in n.choices)
                    {
                        if (string.IsNullOrWhiteSpace(c.text) || !string.IsNullOrEmpty(c.targetNode) && !string.IsNullOrEmpty(c.targetEvent)) throw new InvalidDataException(p + ": 選択肢の文言・接続先が不正です。");
                        Reference(c.targetNode, nodeIds, p + "/choices/" + c.id); Reference(c.targetEvent, eventIds, p + "/choices/" + c.id);
                    }
                }
            }
            foreach (var e in events.events)
            {
                var seen = new HashSet<string>(); var current = e;
                while (!string.IsNullOrEmpty(current.afterEvent)) { if (!seen.Add(current.id)) throw new InvalidDataException("afterEventに循環があります：" + e.id); current = events.events.Single(x => x.id == current.afterEvent); }
            }
        }
        private static void ValidatePlacements(List<ActorPlacement> placements, HashSet<string> ids, string location)
        {
            Unique(placements.Select(p => p?.characterId), location + "/placements");
            foreach (var p in placements) { Reference(p.characterId, ids, location); if (p.position == null || p.rotation == null) throw new InvalidDataException(location + ": 配置座標がありません。"); p.position.Validate(location); p.rotation.Validate(location); if (p.condition != null && !new[] { "preserve", "full", "half", "nude" }.Contains(p.condition.clothing)) throw new InvalidDataException(location + ": 服装状態が不正です。"); p.animation?.Validate(location); }
        }
        private static void Unique(IEnumerable<string> values, string location)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in values) if (string.IsNullOrWhiteSpace(id) || !seen.Add(id)) throw new InvalidDataException(location + ": 空または重複IDです：" + id);
        }
        private static void Reference(string id, HashSet<string> ids, string location) { if (!string.IsNullOrEmpty(id) && !ids.Contains(id)) throw new InvalidDataException(location + ": 参照先がありません：" + id); }
    }
#pragma warning restore CS0649
}
