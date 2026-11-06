using System;
using System.Collections.Generic;
using System.Linq;
using ActionGame;
using UnityEngine;

namespace MainGameAiShoujoMode.Conversation
{
    internal sealed class KksNativeConversationSession
    {
        internal ChaControl Actor { get; }
        internal SaveData.Heroine Heroine { get; }
        internal SaveData.Player Player { get; }
        internal Manager.GameAssist Assist { get; }
        internal KksConversationCatalog.Profile Profile { get; }
        internal KksConversationRuntimeSettings Settings { get; }
        private readonly KksConversationStateStore _store;
        private readonly Dictionary<int, int> _caps = new Dictionary<int, int>();
        private readonly KksConversationCatalog _catalog;
        private KksConversationStateStore.Resident _resident;
        internal string Identity => _resident.id;
        internal int Stage => Mathf.Clamp(Relation, Settings.zero, Settings.maxNormalStage);
        private int Relation => Heroine.isGirlfriend ? (Heroine.favor < _caps[Settings.maxNormalStage] ? Settings.maxNormalStage : Settings.maxNormalStage + Settings.one)
            : Heroine.isFriend ? Settings.one : Settings.zero;
        internal KksNativeConversationSession(ChaControl actor, ChaControl player, string origin, KksConversationCatalog catalog, KksConversationStateStore store)
        {
            Actor = actor ?? throw new ArgumentNullException(nameof(actor)); _catalog = catalog;
            Settings = catalog.Runtime; _store = store;
            if (player == null) throw new InvalidOperationException("会話相手となる主人公が読み込まれていません。");
            Player = new SaveData.Player(player.chaFile, false); Player.SetRoot(player.gameObject);
            var points = store.ReadPlayer().points;
            if (Settings.koikatsuPointMax != SaveData.WorldData.KoikatsuPointMax || points < Settings.zero || points > Settings.koikatsuPointMax)
                throw new InvalidOperationException("AI-mode saved points or native point limits are invalid.");
            Player.koikatsuPoint = points;
            Assist = Singleton<Manager.GameAssist>.Instance;
            if (Assist == null || !Assist.isInit) throw new InvalidOperationException("KKSの会話補助データがまだ読み込まれていません。");
            LoadCaps();
            SaveData.Heroine initialized = null;
            _resident = store.GetOrCreate(origin, () =>
            {
                initialized = new SaveData.Heroine(actor.chaFile, false); initialized.SetRoot(actor.gameObject);
                initialized.callID = Player.callID;
                return new KksConversationStateStore.Resident
                {
                    personality = initialized.personality, favor = initialized.favor, lewdness = initialized.lewdness,
                    friend = initialized.isFriend, girlfriend = initialized.isGirlfriend, callId = initialized.callID,
                    temperament = initialized.talkTemper.ToArray(), events = initialized.talkEvent.ToList(),
                    introductions = initialized.talkIntro.ToList(), topics = initialized.topicTalked.ToList()
                };
            });
            if (initialized == null)
            {
                var random = UnityEngine.Random.state;
                try { initialized = new SaveData.Heroine(actor.chaFile, false); }
                finally { UnityEngine.Random.state = random; }
            }
            Heroine = initialized; Heroine.SetRoot(actor.gameObject); ApplySaved();
            Profile = catalog.Load(Heroine.FixCharaIDOrPersonality);
        }
        private void LoadCaps()
        {
            var files = CommonLib.GetAssetBundleNameListFromPath("action/list/stepup/", true); files.Sort();
            foreach (var file in files)
            {
                try
                {
                    foreach (var asset in AssetBundleManager.LoadAllAsset(file, typeof(StepUpData)).GetAllAssets<StepUpData>().OrderBy(asset => asset.name))
                        foreach (var row in asset.param) _caps[row.no] = row.limit;
                }
                finally { AssetBundleManager.UnloadAssetBundle(file, isUnloadForceRefCount: false); }
            }
            for (var stage = Settings.zero; stage <= Settings.maxNormalStage; stage++)
                if (!_caps.ContainsKey(stage)) throw new InvalidOperationException("KKSの関係段階上限がありません：" + stage);
        }
        private void ApplySaved()
        {
            if (_resident.temperament.Length != _catalog.NormalRules.Configuration.temperamentCount)
                throw new InvalidOperationException("保存済みの会話気質がKKSの形式と一致しません。");
            if (_resident.favor < Settings.zero || _resident.favor > _caps[Settings.maxNormalStage]
                || _resident.lewdness < Settings.zero || _resident.lewdness > Settings.percentMax)
                throw new InvalidOperationException("保存済みのKKS会話の関係値が範囲外です。");
            Heroine.favor = _resident.favor; Heroine.lewdness = _resident.lewdness;
            Heroine.isFriend = _resident.friend; Heroine.isGirlfriend = _resident.girlfriend;
            Heroine.callID = _resident.callId; Heroine.talkTemper = _resident.temperament.ToArray();
            Heroine.talkEvent = new HashSet<int>(_resident.events); Heroine.talkIntro = new HashSet<int>(_resident.introductions);
            Heroine.topicTalked = new HashSet<int>(_resident.topics);
        }
        internal KksNormalConversationRules.Context Context() => new KksNormalConversationRules.Context
        {
            Attribute = ChaInfo.GetAttributeType(Actor.chaFile), NormalNickname = Heroine.isNickNameNormal,
            Events = new HashSet<int>(Heroine.talkEvent), Temperament = Heroine.talkTemper.ToArray(), Preferences = Assist.PreferenceValue(Heroine),
            BodyCategory = category =>
            {
                switch (category)
                {
                    case "Bust": return Actor.GetBustCategory();
                    case "Height": return Actor.GetHeightCategory();
                    case "Waist": return Actor.GetWaistCategory();
                    default: throw new NotSupportedException("Unknown KKS body category: " + category);
                }
            }
        };
        internal KksDialogueProgram Build(KksConversationLayout.BasicInfo block)
        {
            var context = Context();
            if (block is KksConversationLayout.GenericInfo generic) return KksDialogueProgram.Generic(generic);
            if (block is KksConversationLayout.BranchInfo branch)
            {
                var response = _catalog.NormalRules.Branch(branch, context, weights =>
                {
                    var certain = weights.Select((weight, index) => new { weight, index }).FirstOrDefault(value => value.weight == Settings.percentMax);
                    if (certain != null) return certain.index;
                    var roll = UnityEngine.Random.Range(Settings.zero, weights.Sum()); var sum = Settings.zero; var index = Settings.zero;
                    foreach (var weight in weights) { sum += weight; if (roll < sum) return index; index++; }
                    throw new InvalidOperationException("KKS branch weights did not select a response.");
                });
                return KksDialogueProgram.Branch(branch, response, Settings.zero);
            }
            if (block is KksConversationLayout.SelectInfo select)
                return KksDialogueProgram.Select(select, Profile.Options, _catalog.NormalRules.CorrectSelection(select, context), Settings.zero, _catalog.NormalRules.Configuration.fallbackChoice);
            throw new NotSupportedException("Unknown native KKS conversation block.");
        }
        internal void Commit(string operationId, KksConversationLayout.BasicInfo block, KksDialogueProgram.Outcome outcome)
        {
            var preference = Assist.PreferenceValue(Heroine);
            var male = Player.parameter;
            var distance = Mathf.Abs(male.aggressive - preference[Settings.aggressivePreferenceAxis]) + Mathf.Abs(male.diligence - preference[Settings.diligencePreferenceAxis])
                + Mathf.Abs(male.kindness - preference[Settings.kindnessPreferenceAxis]);
            var correction = Assist.correctionInfo;
            var common = correction.preference[Settings.preferenceCorrectionIndex] * Mathf.InverseLerp(Settings.preferenceDistanceMax, Settings.zero, distance);
            var favorRate = common
                + correction.intellect[Settings.favorCorrectionIndex] * Mathf.InverseLerp(Settings.zero, Settings.percentMax, Player.intellect)
                + correction.physical[Settings.favorCorrectionIndex] * Mathf.InverseLerp(Settings.zero, Settings.percentMax, Player.physical)
                + correction.hentai[Settings.favorCorrectionIndex] * Mathf.InverseLerp(Settings.zero, Settings.percentMax, Player.hentai);
            var lewdRate = common + correction.hentai[Settings.lewdnessCorrectionIndex] * Mathf.InverseLerp(Settings.zero, Settings.percentMax, Player.hentai);
            // KKS accommodation/prayer bonuses have no facility in this AI world; do not read another world's Game.saveData.
            var favorDelta = outcome.Favor + (int)(Mathf.Abs(outcome.Favor) * favorRate * (Actor.GetAttribute(Settings.attributeFavorIndex) ? Settings.attributeFavorMultiplier : Settings.one));
            var lewdDelta = outcome.Lewdness + (int)(Mathf.Abs(outcome.Lewdness) * lewdRate);
            var cap = _caps[Stage];
            _resident = _store.Commit(_resident, new KksConversationStateStore.Result
            {
                id = operationId, bundle = Profile.Source.conversation.bundle, asset = Profile.Source.conversation.asset, blockRow = block.row,
                nativeFavor = outcome.Favor, nativeLewdness = outcome.Lewdness,
                appliedFavor = Mathf.Clamp(_resident.favor + favorDelta, Settings.zero, cap) - _resident.favor,
                appliedLewdness = Mathf.Clamp(_resident.lewdness + lewdDelta, Settings.zero, Settings.percentMax) - _resident.lewdness,
                topic = outcome.Topic, selected = outcome.SelectedIndex, correct = outcome.CorrectIndex
            }, resident =>
            {
                resident.personality = Heroine.personality;
                resident.favor = Mathf.Clamp(resident.favor + favorDelta, Settings.zero, cap);
                resident.lewdness = Mathf.Clamp(resident.lewdness + lewdDelta, Settings.zero, Settings.percentMax);
                if (outcome.Topic >= Settings.zero && !resident.topics.Contains(outcome.Topic)) resident.topics.Add(outcome.Topic);
            });
            ApplySaved();
        }
    }
}
