using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using UnityEngine;

namespace MainGameVoiceFaceEventBridge
{
    [BepInPlugin(GUID, PluginName, Version)]
    [BepInProcess("KoikatsuSunshine")]
    internal sealed partial class Plugin : BaseUnityPlugin
    {
        public const string GUID = "com.kks.maingame.voicefaceeventbridge";
        public const string PluginName = "MainGameVoiceFaceEventBridge";
        public const string Version = "0.3.1";

        internal static Plugin Instance { get; private set; }
        internal static new ManualLogSource Logger { get; private set; }
        internal static string PluginDir { get; private set; }
        internal static string LogFilePath { get; private set; }
        internal static PluginSettings Settings { get; private set; }
        internal static HSceneProc CurrentProc { get; private set; }

        private static readonly object FileLogLock = new object();
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);
        private static readonly FieldInfo LstFemaleField = AccessTools.Field(typeof(HSceneProc), "lstFemale");
        private static readonly FieldInfo FaceField = AccessTools.Field(typeof(HSceneProc), "face");
        private static readonly FieldInfo Face1Field = AccessTools.Field(typeof(HSceneProc), "face1");
        private static readonly FieldInfo LstUseAnimInfoField = AccessTools.Field(typeof(HSceneProc), "lstUseAnimInfo");
        private static readonly FieldInfo LstProcField = AccessTools.Field(typeof(HSceneProc), "lstProc");
        private static readonly string[] NormalMissionaryInterlockKeywords = { "密着", "しがみ" };
        private static readonly string[] NormalMissionaryInterlockPoseNames = { "Missionary Interlock" };
        private static readonly string[] StandingWallPreferredPoseNames =
        {
            "壁対面片足上げ",
            "Wall Standing Split",
            "Wall Standings Split"
        };
        private const string PoseScoreRulesFileName = "pose_score_rules.json";
        private const int PoseScoreRulesCurrentVersion = 5;
        private const string DefaultPoseSimpleModeTriggerKeywords = "になるね,の体位になる";
        private const int DefaultPoseScoreBase = 20;
        private const int DefaultPoseAdoptThreshold = 35;
        private const int DefaultPoseForceThreshold = 60;
        private const int DefaultBlankMapAddHttpPort = 55982;
        private static readonly string[] DefaultVideoExtensions =
        {
            ".mp4", ".wmv", ".avi", ".mkv", ".mov", ".m4v", ".webm"
        };

        private sealed class VideoTrackGroup
        {
            public string CanonicalName;
            public string NormalizedName;
            public readonly List<string> FileNames = new List<string>();
        }

        private sealed class VideoTriggerHit
        {
            public int Index;
            public string Keyword;
        }

        private sealed class CameraPresetTriggerHit
        {
            public int LineIndex;
            public int MatchIndex;
            public string TriggerKeyword;
            public string PresetName;
        }

        [DataContract]
        private sealed class PoseKeywordScoreToken
        {
            [DataMember(Name = "keyword")]
            public string Keyword;

            [DataMember(Name = "score")]
            public int Score;
        }

        [DataContract]
        private sealed class PoseKeywordScoreRule
        {
            [DataMember(Name = "id")]
            public string RuleId;

            [DataMember(Name = "category")]
            public string Category;

            [DataMember(Name = "poseNames")]
            public string[] PoseNames;

            [DataMember(Name = "tokens")]
            public PoseKeywordScoreToken[] Tokens;

            [DataMember(Name = "priority")]
            public int Priority;

            [DataMember(Name = "enabled", EmitDefaultValue = false)]
            public bool? Enabled;
        }

        [DataContract]
        private sealed class PoseScoreRulesFile
        {
            [DataMember(Name = "version")]
            public int Version;

            [DataMember(Name = "enabled", EmitDefaultValue = false)]
            public bool? Enabled;

            [DataMember(Name = "simpleModeEnabled", EmitDefaultValue = false)]
            public bool? SimpleModeEnabled;

            [DataMember(Name = "simpleModeTriggerKeywords", EmitDefaultValue = false)]
            public string SimpleModeTriggerKeywords;

            [DataMember(Name = "rulesEnabled", EmitDefaultValue = false)]
            public bool? RulesEnabled;

            [DataMember(Name = "inferRulesEnabled", EmitDefaultValue = false)]
            public bool? InferRulesEnabled;

            [DataMember(Name = "scoreBase")]
            public int ScoreBase;

            [DataMember(Name = "adoptThreshold")]
            public int AdoptThreshold;

            [DataMember(Name = "forceThreshold")]
            public int ForceThreshold;

            [DataMember(Name = "rules")]
            public List<PoseKeywordScoreRule> Rules;

            [DataMember(Name = "inferRules")]
            public List<PoseCategoryInferRule> InferRules;

            [DataMember(Name = "categoryEnabled", EmitDefaultValue = false)]
            public Dictionary<string, bool> CategoryEnabled;

            [DataMember(Name = "categoryAliases", EmitDefaultValue = false)]
            public Dictionary<string, string[]> CategoryAliases;
        }

        [DataContract]
        private sealed class PoseCategoryInferRule
        {
            [DataMember(Name = "id")]
            public string RuleId;

            [DataMember(Name = "targetCategory")]
            public string TargetCategory;

            [DataMember(Name = "priority")]
            public int Priority;

            [DataMember(Name = "requiredAll")]
            public string[] RequiredAll;

            [DataMember(Name = "requiredAny")]
            public string[] RequiredAny;

            [DataMember(Name = "excludeAny")]
            public string[] ExcludeAny;

            [DataMember(Name = "enabled", EmitDefaultValue = false)]
            public bool? Enabled;
        }

        [DataContract]
        private sealed class BlankMapAddSettingsSnapshot
        {
            [DataMember(Name = "FolderPlayPath")]
            public string FolderPlayPath;

            [DataMember(Name = "HttpEnabled", EmitDefaultValue = false)]
            public bool? HttpEnabled;

            [DataMember(Name = "HttpPort", EmitDefaultValue = false)]
            public int? HttpPort;
        }

        private sealed class PoseScoreMatch
        {
            public string Category;
            public PoseKeywordScoreRule Rule;
            public List<PoseCategoryEntry> Candidates;
            public int Score;
            public int LongestMatch;
            public int Priority;
            public int MatchedTokenCount;
        }

        private static readonly PoseKeywordScoreRule[] PoseKeywordScoreRules =
        {
            // 正常位系
            CreatePoseScoreRule(
                "normal_missionary_interlock",
                "正常位系",
                220,
                new[] { "Missionary Interlock" },
                CreatePoseScoreToken("密着", 40),
                CreatePoseScoreToken("しがみ", 40)),
            CreatePoseScoreRule(
                "normal_breeding_press",
                "正常位系",
                210,
                new[] { "種付けプレス" },
                CreatePoseScoreToken("種付け", 40),
                CreatePoseScoreToken("孕ませ", 40)),
            CreatePoseScoreRule(
                "normal_kaikyaku",
                "正常位系",
                120,
                new[] { "開脚正常位" },
                CreatePoseScoreToken("開脚", 25),
                CreatePoseScoreToken("開", 10),
                CreatePoseScoreToken("脚", 10),
                CreatePoseScoreToken("足", 10)),
            CreatePoseScoreRule(
                "normal_manguri",
                "正常位系",
                160,
                new[] { "マングリ正常位" },
                CreatePoseScoreToken("マングリ", 40)),
            CreatePoseScoreRule(
                "normal_table",
                "正常位系",
                115,
                new[] { "卓球台正常位" },
                CreatePoseScoreToken("台", 25)),
            CreatePoseScoreRule(
                "normal_knee_hold",
                "正常位系",
                116,
                new[] { "膝抱え正常位" },
                CreatePoseScoreToken("膝", 10),
                CreatePoseScoreToken("抱え", 10)),
            CreatePoseScoreRule(
                "normal_leg_open_wide",
                "正常位系",
                117,
                new[] { "大股開き正常位" },
                CreatePoseScoreToken("股", 25),
                CreatePoseScoreToken("開き", 25),
                CreatePoseScoreToken("開", 10)),
            CreatePoseScoreRule(
                "normal_hip_hold",
                "正常位系",
                116,
                new[] { "腰抱え正常位" },
                CreatePoseScoreToken("腰", 10),
                CreatePoseScoreToken("抱え", 10)),
            CreatePoseScoreRule(
                "normal_bridge",
                "正常位系",
                116,
                new[] { "ブリッジ正常位" },
                CreatePoseScoreToken("ブリッジ", 25)),
            CreatePoseScoreRule(
                "normal_beachball",
                "正常位系",
                116,
                new[] { "ビーチボール正常位" },
                CreatePoseScoreToken("ビーチ", 25),
                CreatePoseScoreToken("ボール", 10)),

            // 後背位系
            CreatePoseScoreRule(
                "back_arm_pull",
                "後背位系",
                170,
                new[] { "腕引っ張り後背位", "Doggystyle Hair-Pull Forced", "Doggy Standing Arm-Grab" },
                CreatePoseScoreToken("腕", 10),
                CreatePoseScoreToken("手", 10),
                CreatePoseScoreToken("引っ張", 40)),
            CreatePoseScoreRule(
                "back_push_against",
                "後背位系",
                175,
                new[] { "押し付けバック" },
                CreatePoseScoreToken("壁", 25),
                CreatePoseScoreToken("押し付け", 40)),
            CreatePoseScoreRule(
                "back_fence",
                "後背位系",
                174,
                new[] { "フェンス後背位" },
                CreatePoseScoreToken("フェンス", 40)),
            CreatePoseScoreRule(
                "back_banana",
                "後背位系",
                118,
                new[] { "バナナボート後背位" },
                CreatePoseScoreToken("バナナ", 25),
                CreatePoseScoreToken("ボート", 10)),
            CreatePoseScoreRule(
                "back_net",
                "後背位系",
                118,
                new[] { "ネット後背位" },
                CreatePoseScoreToken("ネット", 25)),
            CreatePoseScoreRule(
                "back_prone",
                "後背位系",
                118,
                new[] { "伏せ後背位" },
                CreatePoseScoreToken("伏せ", 25)),
            CreatePoseScoreRule(
                "back_float_ring",
                "後背位系",
                118,
                new[] { "浮き輪後背位" },
                CreatePoseScoreToken("浮き輪", 25),
                CreatePoseScoreToken("浮き", 10),
                CreatePoseScoreToken("輪", 10)),
            CreatePoseScoreRule(
                "back_leg_hold",
                "後背位系",
                118,
                new[] { "足抱え後背位" },
                CreatePoseScoreToken("足", 10),
                CreatePoseScoreToken("抱え", 10)),

            // 騎乗位系
            CreatePoseScoreRule(
                "cowgirl_hug",
                "騎乗位系",
                180,
                new[] { "Cowgirl Hug" },
                CreatePoseScoreToken("密着", 40)),
            CreatePoseScoreRule(
                "cowgirl_sofa",
                "騎乗位系",
                118,
                new[] { "ソファ騎乗位" },
                CreatePoseScoreToken("ソファ", 25)),
            CreatePoseScoreRule(
                "cowgirl_hand_hold",
                "騎乗位系",
                118,
                new[] { "手つなぎ騎乗位", "Hand-Holding Cowgirl 2", "Hand-Holding Cowgirl 3" },
                CreatePoseScoreToken("手", 10),
                CreatePoseScoreToken("つなぎ", 25)),
            CreatePoseScoreRule(
                "cowgirl_nipple",
                "騎乗位系",
                118,
                new[] { "乳首責め騎乗位", "Cowgirl Nipple Torture 2" },
                CreatePoseScoreToken("乳首", 40),
                CreatePoseScoreToken("責め", 25)),
            CreatePoseScoreRule(
                "cowgirl_banana",
                "騎乗位系",
                118,
                new[] { "バナナボート騎乗位" },
                CreatePoseScoreToken("バナナ", 25),
                CreatePoseScoreToken("ボート", 10)),

            // 背面騎乗位系
            CreatePoseScoreRule(
                "reverse_cowgirl_wall_hand",
                "背面騎乗位系",
                150,
                new[] { "壁手つき背面騎乗位", "Reverse Cowgirl", "Reverse Cowgirl 3", "Reverse Cowgirl 4", "Reverse Cowgirl 5" },
                CreatePoseScoreToken("壁", 40),
                CreatePoseScoreToken("手つき", 25),
                CreatePoseScoreToken("手", 10),
                CreatePoseScoreToken("つき", 10),
                CreatePoseScoreToken("背中", 25),
                CreatePoseScoreToken("後ろ", 25),
                CreatePoseScoreToken("跨", 25)),

            // 側位(測位)系
            CreatePoseScoreRule(
                "side_desk",
                "測位系",
                118,
                new[] { "机側位" },
                CreatePoseScoreToken("机", 25)),
            CreatePoseScoreRule(
                "side_back",
                "測位系",
                118,
                new[] { "背面側位" },
                CreatePoseScoreToken("背面", 25),
                CreatePoseScoreToken("後ろ", 25)),
            CreatePoseScoreRule(
                "side_princess",
                "測位系",
                180,
                new[] { "お姫様抱っこ側位", "Princess Hug Side Position 2" },
                CreatePoseScoreToken("お姫様", 40),
                CreatePoseScoreToken("抱っこ", 25)),

            // 座位系
            CreatePoseScoreRule(
                "sitting_floor_face",
                "座位系",
                118,
                new[] { "床対面座位" },
                CreatePoseScoreToken("床", 25),
                CreatePoseScoreToken("対面", 25)),
            CreatePoseScoreRule(
                "sitting_foot_hook_face",
                "座位系",
                118,
                new[] { "足掛け対面座位" },
                CreatePoseScoreToken("足掛け", 25),
                CreatePoseScoreToken("足", 10),
                CreatePoseScoreToken("掛け", 10),
                CreatePoseScoreToken("対面", 25)),
            CreatePoseScoreRule(
                "sitting_floor_back",
                "座位系",
                118,
                new[] { "床背面座位" },
                CreatePoseScoreToken("床", 25),
                CreatePoseScoreToken("背面", 25)),
            CreatePoseScoreRule(
                "sitting_seiza_back",
                "座位系",
                118,
                new[] { "正座背面座位" },
                CreatePoseScoreToken("正座", 25),
                CreatePoseScoreToken("背面", 25)),
            CreatePoseScoreRule(
                "sitting_knee_up_back",
                "座位系",
                118,
                new[] { "膝立て背面座位" },
                CreatePoseScoreToken("膝立て", 25),
                CreatePoseScoreToken("膝", 10),
                CreatePoseScoreToken("立て", 10),
                CreatePoseScoreToken("背面", 25)),

            // 立位系 / 立後背位系
            CreatePoseScoreRule(
                "standing_wall_priority",
                "立位系",
                170,
                new[] { "壁対面片足上げ", "Wall Standing Split", "Wall Standings Split" },
                CreatePoseScoreToken("壁", 40)),
            CreatePoseScoreRule(
                "standing_table",
                "立位系",
                118,
                new[] { "卓球台立位" },
                CreatePoseScoreToken("台", 25)),
            CreatePoseScoreRule(
                "standing_back_default",
                "立後背位系",
                140,
                new[] { "立ちバック", "Standing Doggystyle", "Standing From Behind 2", "Doggy Standing Arm-Grab", "Wall Kneeling Doggystyle" },
                CreatePoseScoreToken("立ち", 10),
                CreatePoseScoreToken("立位", 10),
                CreatePoseScoreToken("後ろ", 25),
                CreatePoseScoreToken("バック", 25),
                CreatePoseScoreToken("背後", 25)),

            // オナニー系
            CreatePoseScoreRule(
                "masturbation_plain",
                "オナニー系",
                180,
                new[] { "角オナニー", "椅子オナニー", "立ちオナニー", "トイレオナニー", "シャワーオナニー", "床オナニー", "ネットオナニー", "ディルドオナニー", "ローターオナニー" },
                CreatePoseScoreToken("オナニー", 40),
                CreatePoseScoreToken("自慰", 40),
                CreatePoseScoreToken("ひとり", 20),
                CreatePoseScoreToken("一人", 20),
                CreatePoseScoreToken("セルフ", 20),
                CreatePoseScoreToken("自分で", 20)),
            CreatePoseScoreRule(
                "masturbation_device_rotor",
                "オナニー系",
                220,
                new[] { "ローターオナニー" },
                CreatePoseScoreToken("ローター", 40)),
            CreatePoseScoreRule(
                "masturbation_device_dildo",
                "オナニー系",
                220,
                new[] { "ディルドオナニー" },
                CreatePoseScoreToken("ディルド", 40)),
            CreatePoseScoreRule(
                "masturbation_location_shower",
                "オナニー系",
                210,
                new[] { "シャワーオナニー" },
                CreatePoseScoreToken("シャワー", 40)),
            CreatePoseScoreRule(
                "masturbation_location_toilet",
                "オナニー系",
                210,
                new[] { "トイレオナニー" },
                CreatePoseScoreToken("トイレ", 40)),
            CreatePoseScoreRule(
                "masturbation_location_stand",
                "オナニー系",
                200,
                new[] { "立ちオナニー" },
                CreatePoseScoreToken("立ち", 20),
                CreatePoseScoreToken("立って", 20)),
            CreatePoseScoreRule(
                "masturbation_location_chair",
                "オナニー系",
                200,
                new[] { "椅子オナニー" },
                CreatePoseScoreToken("椅子", 30)),
            CreatePoseScoreRule(
                "masturbation_location_floor",
                "オナニー系",
                200,
                new[] { "床オナニー" },
                CreatePoseScoreToken("床", 30)),
            CreatePoseScoreRule(
                "masturbation_location_corner",
                "オナニー系",
                200,
                new[] { "角オナニー" },
                CreatePoseScoreToken("角", 30)),
            CreatePoseScoreRule(
                "masturbation_location_net",
                "オナニー系",
                200,
                new[] { "ネットオナニー" },
                CreatePoseScoreToken("ネット", 30))
        };

        private static readonly string[] PoseInferCommonExclude = { "しない", "じゃない", "やめる", "止める", "やらない" };
        private static readonly string[] PoseInferCowgirlExclude = { "しない", "じゃない", "やめる", "止める", "やらない", "背面", "逆騎乗", "後ろ向き" };
        private static readonly string[] PoseInferMasturbationExclude = { "しない", "じゃない", "やめる", "止める", "やらない", "奉仕", "ご奉仕", "フェラ", "手コキ", "パイズリ", "挿れ", "突い", "ピストン", "中に" };
        private static readonly PoseCategoryInferRule[] PoseCategoryInferenceRules =
        {
            // 正常位系
            CreatePoseInferRule(
                "infer_normal_supine_leg_open",
                "正常位系",
                160,
                new[] { "仰向け", "脚" },
                new[] { "開", "広げ" },
                PoseInferCommonExclude),
            CreatePoseInferRule(
                "infer_normal_supine_foot_open",
                "正常位系",
                160,
                new[] { "仰向け", "足" },
                new[] { "開", "広げ" },
                PoseInferCommonExclude),
            CreatePoseInferRule(
                "infer_normal_plain_word",
                "正常位系",
                150,
                new string[0],
                new[] { "正常位", "missionary", "Missionary" },
                PoseInferCommonExclude),
            CreatePoseInferRule(
                "infer_normal_hug_interlock",
                "正常位系",
                155,
                new[] { "密着" },
                new[] { "しがみ", "抱きつ", "抱き合" },
                PoseInferCommonExclude),
            CreatePoseInferRule(
                "infer_normal_seed_press",
                "正常位系",
                170,
                new string[0],
                new[] { "種付け", "孕ませ" },
                PoseInferCommonExclude),

            // 後背位系
            CreatePoseInferRule(
                "infer_back_plain_word",
                "後背位系",
                155,
                new string[0],
                new[] { "後背位", "バック", "後ろから", "背後", "doggy", "Doggystyle", "from behind", "From Behind" },
                PoseInferCommonExclude),
            CreatePoseInferRule(
                "infer_back_arm_pull",
                "後背位系",
                170,
                new[] { "引っ張" },
                new[] { "腕", "手" },
                PoseInferCommonExclude),
            CreatePoseInferRule(
                "infer_back_wall_push",
                "後背位系",
                175,
                new[] { "壁" },
                new[] { "押し付け", "押しつけ", "押し当て", "押し当てる" },
                PoseInferCommonExclude),
            CreatePoseInferRule(
                "infer_back_fours",
                "後背位系",
                145,
                new string[0],
                new[] { "四つん這い", "四つんばい", "伏せ", "うつ伏せ" },
                PoseInferCommonExclude),

            // 騎乗位系
            CreatePoseInferRule(
                "infer_cowgirl_plain_word",
                "騎乗位系",
                155,
                new string[0],
                new[] { "騎乗位", "騎乗" },
                PoseInferCowgirlExclude),
            CreatePoseInferRule(
                "infer_cowgirl_mount_motion",
                "騎乗位系",
                150,
                new string[0],
                new[] { "またが", "跨", "上に乗", "乗って", "乗る" },
                PoseInferCowgirlExclude),
            CreatePoseInferRule(
                "infer_cowgirl_hug_motion",
                "騎乗位系",
                165,
                new[] { "密着" },
                new[] { "またが", "跨", "上に乗", "乗って", "乗る" },
                PoseInferCowgirlExclude),

            // 背面騎乗位系
            CreatePoseInferRule(
                "infer_reverse_cowgirl_plain_word",
                "背面騎乗位系",
                165,
                new string[0],
                new[] { "背面騎乗位", "背面騎乗", "逆騎乗位", "逆騎乗" },
                PoseInferCommonExclude),
            CreatePoseInferRule(
                "infer_reverse_cowgirl_direction_mount",
                "背面騎乗位系",
                160,
                new string[0],
                new[] { "後ろ向き", "背中を向け", "背中向け", "後ろ向いて", "後ろ", "背中" },
                PoseInferCommonExclude),
            CreatePoseInferRule(
                "infer_reverse_cowgirl_mount_word",
                "背面騎乗位系",
                150,
                new[] { "後ろ" },
                new[] { "またが", "跨", "乗って", "乗る" },
                PoseInferCommonExclude),

            // 測位系
            CreatePoseInferRule(
                "infer_side_plain_word",
                "測位系",
                150,
                new string[0],
                new[] { "側位", "横向き", "横で", "横から" },
                PoseInferCommonExclude),
            CreatePoseInferRule(
                "infer_side_princess",
                "測位系",
                170,
                new string[0],
                new[] { "お姫様", "抱っこ", "姫抱っこ" },
                PoseInferCommonExclude),
            CreatePoseInferRule(
                "infer_side_lie_down",
                "測位系",
                140,
                new[] { "横" },
                new[] { "寝", "絡め" },
                PoseInferCommonExclude),

            // 座位系
            CreatePoseInferRule(
                "infer_sitting_plain_word",
                "座位系",
                150,
                new string[0],
                new[] { "座位", "座って", "座る", "座ったまま", "椅子" },
                PoseInferCommonExclude),
            CreatePoseInferRule(
                "infer_sitting_face",
                "座位系",
                155,
                new[] { "対面" },
                new[] { "座位", "座って", "椅子", "床", "向かい合" },
                PoseInferCommonExclude),
            CreatePoseInferRule(
                "infer_sitting_back",
                "座位系",
                155,
                new string[0],
                new[] { "背面座位", "正座", "膝立て", "床背面", "正座背面", "膝立て背面" },
                PoseInferCommonExclude),

            // 立位系
            CreatePoseInferRule(
                "infer_standing_plain_word",
                "立位系",
                150,
                new string[0],
                new[] { "立位", "立って", "立ったまま", "駅弁" },
                PoseInferCommonExclude),
            CreatePoseInferRule(
                "infer_standing_wall",
                "立位系",
                165,
                new[] { "壁" },
                new[] { "立位", "立って", "片足", "立ったまま" },
                PoseInferCommonExclude),

            // 立後背位系
            CreatePoseInferRule(
                "infer_standing_back_plain_word",
                "立後背位系",
                170,
                new string[0],
                new[] { "立ちバック", "立位バック", "Standing Doggystyle", "standing doggy", "standing from behind" },
                PoseInferCommonExclude),
            CreatePoseInferRule(
                "infer_standing_back_combo",
                "立後背位系",
                165,
                new[] { "後ろ" },
                new[] { "立って", "立位", "立ち", "背後", "バック" },
                PoseInferCommonExclude),
            CreatePoseInferRule(
                "infer_standing_back_wall",
                "立後背位系",
                175,
                new[] { "壁" },
                new[] { "後ろ", "背後", "バック", "押し付け" },
                PoseInferCommonExclude),

            // オナニー系
            CreatePoseInferRule(
                "infer_masturbation_plain_word",
                "オナニー系",
                190,
                new string[0],
                new[] { "オナニー", "自慰", "ひとりで", "一人で", "セルフ", "自分で", "masturbation", "Masturbation" },
                PoseInferMasturbationExclude),
            CreatePoseInferRule(
                "infer_masturbation_device",
                "オナニー系",
                200,
                new string[0],
                new[] { "ローター", "ディルド" },
                PoseInferMasturbationExclude),
            CreatePoseInferRule(
                "infer_masturbation_location",
                "オナニー系",
                180,
                new[] { "オナニー" },
                new[] { "角", "椅子", "立ち", "立って", "トイレ", "シャワー", "床", "ネット" },
                PoseInferMasturbationExclude)
        };

        private readonly Queue<ExternalVoiceFaceCommand> _commandQueue = new Queue<ExternalVoiceFaceCommand>();
        private readonly Queue<ExternalVoiceFaceCommand> _responseTextQueue = new Queue<ExternalVoiceFaceCommand>();
        private readonly object _queueLock = new object();
        private readonly System.Random _random = new System.Random();
        private readonly object _facePresetCacheLock = new object();
        private readonly Dictionary<string, float> _blockLogCooldownByKey = new Dictionary<string, float>();
        private string _facePresetCachePath = string.Empty;
        private DateTime _facePresetCacheWriteTimeUtc = DateTime.MinValue;
        private long _facePresetCacheLength = -1;
        private List<FacePresetJsonItem> _facePresetCache = new List<FacePresetJsonItem>();
        private FacePresetProbeState _facePresetProbe;
        private int _facePresetProbeSequence;

        private readonly List<Tuple<float, Action>> _delayedActions = new List<Tuple<float, Action>>();
        private readonly Dictionary<string, List<PoseCategoryEntry>> _poseEntriesByCategory =
            new Dictionary<string, List<PoseCategoryEntry>>(StringComparer.Ordinal);
        private Coroutine _responseTextCoroutine;
        private int _poseScoreBase = DefaultPoseScoreBase;
        private int _poseAdoptThreshold = DefaultPoseAdoptThreshold;
        private int _poseForceThreshold = DefaultPoseForceThreshold;
        private List<PoseKeywordScoreRule> _poseKeywordScoreRules = new List<PoseKeywordScoreRule>();
        private List<PoseCategoryInferRule> _poseCategoryInferRules = new List<PoseCategoryInferRule>();
        private Dictionary<string, string[]> _categoryAliases = new Dictionary<string, string[]>(StringComparer.Ordinal);
        private bool _poseChangeEnabled = true;
        private bool _poseSimpleModeEnabled = false;
        private string _poseSimpleModeTriggerKeywords = DefaultPoseSimpleModeTriggerKeywords;
        private bool _poseRulesEnabled = true;
        private bool _poseInferRulesEnabled = true;
        private bool _poseCategoriesExpanded = false;
        private bool _poseRulesExpanded = false;
        private bool _poseInferRulesExpanded = false;
        private readonly Dictionary<string, bool> _poseCategoryEnabled = new Dictionary<string, bool>(StringComparer.Ordinal);
        private string _poseScoreRulesFilePath;
        private static Type _configurationManagerType;
        private static MethodInfo _configurationManagerBuildSettingListMethod;

        private const string PoseSonyuClassifiedFileName = "pose_sonyu_classified.json";
        private const string PoseHoushiClassifiedFileName = "pose_houshi_classified.json";
        private const string PoseMasturbationClassifiedFileName = "pose_masturbation_classified.json";
        private const string PoseListFileName = "pose_list.json";
        private const string PoseListTranslatedFileName = "pose_list_ja_with_original.json";
        private const string DefaultFacePresetJsonRelativePath = "..\\..\\StudioFacePresetTool\\StudioFacePresets.json";
        private static readonly string[] SonyuCategoryNames =
        {
            "正常位系",
            "騎乗位系",
            "背面騎乗位系",
            "後背位系",
            "座位系",
            "測位系",
            "立位系",
            "立後背位系"
        };
        private static readonly string[] HoushiCategoryNames =
        {
            "フェラ系",
            "手コキ系",
            "パイズリ系",
            "クンニ系",
            "69系",
            "足コキ系",
            "顔面騎乗系",
            "キス・愛撫系"
        };
        private static readonly string[] MasturbationCategoryNames =
        {
            "オナニー系"
        };
        private const string PoseControlSectionName = "体位制御";
        private const string PoseControlCategoriesSectionName = "体位制御.カテゴリ";
        private const string PoseControlRulesSectionName = "体位制御.ルール";
        private const string PoseControlInferRulesSectionName = "体位制御.推定";

        [DataContract]
        private sealed class PoseClassificationFile
        {
            [DataMember(Name = "categories")]
            public Dictionary<string, List<PoseClassificationItem>> Categories;
        }

        [DataContract]
        private sealed class PoseListFile
        {
            [DataMember(Name = "generatedAt")]
            public string GeneratedAt;

            [DataMember(Name = "source")]
            public string Source;

            [DataMember(Name = "poses")]
            public List<PoseListItem> Poses;
        }

        [DataContract]
        private sealed class PoseListItem
        {
            [DataMember(Name = "id")]
            public int Id;

            [DataMember(Name = "mode")]
            public string Mode;

            [DataMember(Name = "modeInt")]
            public int ModeInt;

            [DataMember(Name = "nameAnimation")]
            public string NameAnimation;
        }

        [DataContract]
        private sealed class PoseTranslatedListFile
        {
            [DataMember(Name = "poses")]
            public List<PoseTranslatedListItem> Poses;
        }

        [DataContract]
        private sealed class PoseTranslatedListItem
        {
            [DataMember(Name = "modeInt")]
            public int ModeInt;

            [DataMember(Name = "nameAnimation")]
            public string NameAnimation;

            [DataMember(Name = "nameAnimationOriginal")]
            public string NameAnimationOriginal;
        }

        [DataContract]
        private sealed class PoseClassificationItem
        {
            [DataMember(Name = "nameAnimation")]
            public string NameAnimation;

            [DataMember(Name = "modeInt")]
            public int ModeInt;
        }

        [DataContract]
        private sealed class FacePresetJsonFile
        {
            [DataMember(Name = "Presets")]
            public List<FacePresetJsonItem> Presets = new List<FacePresetJsonItem>();
        }

        [DataContract]
        private sealed class FacePresetJsonItem
        {
            [DataMember(Name = "Id")]
            public string Id;

            [DataMember(Name = "Name")]
            public string Name;

            [DataMember(Name = "Eyebrow")]
            public int Eyebrow;

            [DataMember(Name = "Eye")]
            public int Eye;

            [DataMember(Name = "Mouth")]
            public int Mouth;

            [DataMember(Name = "EyeMin")]
            public float EyeMin;

            [DataMember(Name = "EyeMax")]
            public float EyeMax = 1f;

            [DataMember(Name = "MouthMin")]
            public float MouthMin;

            [DataMember(Name = "MouthMax")]
            public float MouthMax = 1f;

            [DataMember(Name = "Tears")]
            public int Tears;

            [DataMember(Name = "Cheek")]
            public float Cheek;
        }

        private sealed class FaceSnapshot
        {
            public int Eyebrow;
            public int Eyes;
            public int Mouth;
            public float EyesOpenMin;
            public float EyesOpenMax;
            public float MouthOpenMin;
            public float MouthOpenMax;
            public float MouthFixedRate;
            public int Tears;
            public float Cheek;
        }

        private sealed class FacePresetProbeState
        {
            public int ProbeId;
            public ChaControl Target;
            public string RequestedName;
            public string RequestedId;
            public bool RequestedRandom;
            public string SelectedName;
            public string SelectedId;
            public int StartFrame;
            public int ExpireFrame;
            public float StartTime;
            public FaceSnapshot Baseline;
            public FaceSnapshot Last;
            public int ChangeLogCount;
            public int HookLogCount;
            public int LastHookFrame;
            public string LastHookKey;
        }

        private sealed class PoseCategoryEntry
        {
            public string NameAnimation;
            public int ModeInt;
        }

        private sealed class PoseContinuationState
        {
            public bool ShouldContinue;
            public bool ShouldRestoreAuto;
            public HFlag.EMode PreviousMode;
            public HFlag.EMode TargetMode;
            public int TargetId;
            public string TargetName;
            public string PreviousStateName;
            public bool TargetSonyu;
            public bool TargetHoushi;
            public bool WasAnal;
            public bool WasLoop;
            public bool WasStrongLoop;
            public float SpeedCalc;
            public int SpeedHoushi;
            public bool VoiceSpeedMotion;
            public bool WasAuto;
            public int Attempts;
        }

        private sealed class TimedClothesItem
        {
            public ClothesItem Item;
            public int MatchIndex;
            public string PartKeyword;
            public string ActionKeyword;
        }

        private sealed class TimedCoordItem
        {
            public string CoordName;
            public int MatchIndex;
            public string TriggerKeyword;
        }

        private sealed class TextLineSpan
        {
            public string Line;
            public int StartIndex;
        }

        private Harmony _harmony;
        private ExternalPipeServer _pipeServer;
        private ExternalVoicePlayer _externalVoicePlayer;
        private float _nextProcProbeTime;
        private float _blockGameVoiceUntil;
        private bool _voiceProcStopOverridden;
        private bool _voiceProcStopOriginal;
        private float _nextVoiceRestorePendingLogTime;
        private string _activeSequenceSessionId = string.Empty;

        private ConfigEntry<bool> _cfgEnabled;
        private ConfigEntry<bool> _cfgVerboseLog;
        private ConfigEntry<KeyboardShortcut> _cfgReloadKey;
        private ConfigEntry<KeyboardShortcut> _cfgStateDumpKey;
        private ConfigEntry<float> _cfgPlaybackVolume;
        private ConfigEntry<float> _cfgFemalePlaybackVolume;
        private ConfigEntry<float> _cfgExternalPlaybackPitch;

        // ClothesDetection
        private ConfigEntry<string> _cfgTopKeywords;
        private ConfigEntry<string> _cfgBottomKeywords;
        private ConfigEntry<string> _cfgBraKeywords;
        private ConfigEntry<string> _cfgShortsKeywords;
        private ConfigEntry<string> _cfgGlovesKeywords;
        private ConfigEntry<string> _cfgPanthoseKeywords;
        private ConfigEntry<string> _cfgSocksKeywords;
        private ConfigEntry<string> _cfgShoesKeywords;
        private ConfigEntry<string> _cfgGlassesKeywords;
        private ConfigEntry<string> _cfgRemoveKeywords;
        private ConfigEntry<string> _cfgShiftKeywords;
        private ConfigEntry<string> _cfgPutOnKeywords;
        private ConfigEntry<string> _cfgRemoveAllKeywords;
        private ConfigEntry<string> _cfgPutOnAllKeywords;
        private ConfigEntry<string> _cfgCoordPattern;
        private ConfigEntry<string> _cfgCameraTriggerKeywords;
        private ConfigEntry<bool> _cfgEnableVideoPlaybackByResponseText;
        private ConfigEntry<string> _cfgVideoPlaybackTriggerKeywords;
        private ConfigEntry<bool> _cfgSequenceSubtitleEnabled;
        private ConfigEntry<string> _cfgSequenceSubtitleHost;
        private ConfigEntry<int> _cfgSequenceSubtitlePort;
        private ConfigEntry<string> _cfgSequenceSubtitleEndpointPath;
        private ConfigEntry<string> _cfgSequenceSubtitleDisplayMode;
        private ConfigEntry<string> _cfgSequenceSubtitleSendMode;
        private ConfigEntry<float> _cfgSequenceSubtitleHoldPaddingSeconds;
        private ConfigEntry<bool> _cfgSequenceSubtitleProgressPrefixEnabled;
        private ConfigEntry<bool> _cfgEnableFacePresetApply;
        private ConfigEntry<string> _cfgFacePresetJsonRelativePath;
        private ConfigEntry<bool> _cfgPoseChangeEnabled;
        private ConfigEntry<bool> _cfgPoseSimpleModeEnabled;
        private ConfigEntry<string> _cfgPoseSimpleModeTriggerKeywords;
        private ConfigEntry<bool> _cfgPoseRulesEnabled;
        private ConfigEntry<bool> _cfgPoseInferRulesEnabled;
        private ConfigEntry<bool> _cfgPoseCategoriesExpanded;
        private ConfigEntry<bool> _cfgPoseRulesExpanded;
        private ConfigEntry<bool> _cfgPoseInferRulesExpanded;
        private readonly Dictionary<string, ConfigEntry<bool>> _cfgPoseCategoryEnabledEntries = new Dictionary<string, ConfigEntry<bool>>(StringComparer.Ordinal);
        private readonly Dictionary<string, ConfigEntry<bool>> _cfgPoseRuleEnabledEntries = new Dictionary<string, ConfigEntry<bool>>(StringComparer.Ordinal);
        private readonly Dictionary<string, ConfigEntry<bool>> _cfgPoseInferRuleEnabledEntries = new Dictionary<string, ConfigEntry<bool>>(StringComparer.Ordinal);
        private readonly Dictionary<ConfigEntryBase, ConfigurationManager.ConfigurationManagerAttributes> _cfgPoseReadonlyAttributes =
            new Dictionary<ConfigEntryBase, ConfigurationManager.ConfigurationManagerAttributes>();
        private readonly Dictionary<string, List<string>> _poseNameAliasesByCanonical =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private bool _suppressConfigChangeEvent;
        private bool _suppressPoseConfigChangeEvent;

        private void Awake()
        {
            Instance = this;
            Logger = base.Logger;
            PluginDir = Path.GetDirectoryName(Info.Location);
            LogFilePath = Path.Combine(PluginDir, PluginName + ".log");

            Directory.CreateDirectory(PluginDir);
            File.WriteAllText(
                LogFilePath,
                $"[{DateTime.Now:HH:mm:ss}] === {PluginName} {Version} started ==={Environment.NewLine}",
                Utf8NoBom);

            Settings = SettingsStore.LoadOrCreate(PluginDir, Log, LogWarn, LogError);
            LoadScenarioTextRules();
            BindConfigEntries();
            ApplySettingsToConfigEntries(Settings);
            SaveConfigFile(reason: "awake");
            LoadPoseScoreRules();
            LoadPoseCategoryEntries();
            _externalVoicePlayer = new ExternalVoicePlayer(Log, LogWarn, LogError);
            _externalVoicePlayer.PlaybackStarted += OnExternalVoicePlaybackStarted;
            LogGuardSettings("awake");

            _harmony = new Harmony(GUID);
            _harmony.PatchAll(typeof(Plugin));
            VoiceGuardHooks.Apply(_harmony, Log, LogWarn, LogError);
            FacePresetProbeHooks.Apply(_harmony, Log, LogWarn, LogError);

            StartOrRestartPipeServer(forceRestart: true, reason: "awake");
            StartWatchdog();
            Log("awake complete");
        }

        private void StartWatchdog()
        {
            _watchdogRunning = true;
            System.Threading.Thread t = new System.Threading.Thread(() =>
            {
                int lastFrame = 0;
                while (_watchdogRunning)
                {
                    System.Threading.Thread.Sleep(2000);
                    int cur = _mainThreadFrameCount;
                    int delta = cur - lastFrame;
                    lastFrame = cur;
                    bool extPlaying = _externalVoicePlayer != null && _externalVoicePlayer.IsPlaying;
                    if (Settings != null && Settings.VerboseLog)
                    {
                        Log($"[watchdog] frames+2s={delta} extPlaying={extPlaying} lastStep={_updateLastStep}");
                    }
                }
            });
            t.IsBackground = true;
            t.Name = "MainGameVoiceFaceEventBridge.Watchdog";
            t.Start();
        }

        private void OnDestroy()
        {
            _watchdogRunning = false;
            StopPipeServer("on_destroy");
            ResetResponseTextQueue("on_destroy");

            try
            {
                _harmony?.UnpatchSelf();
                _harmony = null;
            }
            catch (Exception ex)
            {
                LogWarn("unpatch failed: " + ex.Message);
            }

            if (_externalVoicePlayer != null)
            {
                _externalVoicePlayer.PlaybackStarted -= OnExternalVoicePlaybackStarted;
                _externalVoicePlayer.Dispose();
                _externalVoicePlayer = null;
            }

            CurrentProc = null;
        }

        private float _updateProbeNext;
        private int _updateLastStep;
        private volatile int _mainThreadFrameCount;
        private volatile bool _watchdogRunning;
        private float _nextStateDumpAllowedTime;
        private string _lastResponseTraceId = "(none)";
        private string _lastResponsePhase = "idle";
        private float _lastResponseStartedAt = -1f;
        private float _lastResponseElapsedMs = -1f;
        private int _lastResponseTextLength;

        private void Update()
        {
            float now = Time.realtimeSinceStartup;
            bool extPlaying = _externalVoicePlayer != null && _externalVoicePlayer.IsPlaying;
            if (extPlaying && now >= _updateProbeNext)
            {
                _updateProbeNext = now + 2f;
                if (Settings != null && Settings.VerboseLog)
                {
                    Log($"[update-probe] alive t={now:F1} lastStep={_updateLastStep}");
                }
                _updateLastStep = 0;
            }

            if (Settings != null && Settings.EnableCtrlRReload && IsReloadKeyDown())
            {
                ReloadSettings();
            }

            if (IsStateDumpKeyDown() && now >= _nextStateDumpAllowedTime)
            {
                _nextStateDumpAllowedTime = now + 0.5f;
                DumpRuntimeState("manual_hotkey");
            }

            _mainThreadFrameCount++;
            if (extPlaying) _updateLastStep = 1;
            bool wasExternalPlaying = _externalVoicePlayer != null && _externalVoicePlayer.IsPlaying;
            _externalVoicePlayer?.Update();
            if (extPlaying) _updateLastStep = 2;
            bool isExternalPlaying = _externalVoicePlayer != null && _externalVoicePlayer.IsPlaying;
            if (wasExternalPlaying && !isExternalPlaying)
            {
                _blockGameVoiceUntil = 0f;
                Log("[guard] external play finished -> clear block and try restore");
                RestoreVoiceProcStopIfNeeded();
            }

            if (!ShouldBlockGameVoiceEvents())
            {
                RestoreVoiceProcStopIfNeeded();
            }
            if (extPlaying) _updateLastStep = 3;

            if (Settings == null || !Settings.Enabled)
            {
                if (_responseTextCoroutine != null || _responseTextQueue.Count > 0)
                {
                    ResetResponseTextQueue("settings_disabled");
                }
                return;
            }

            if (extPlaying) _updateLastStep = 4;
            ProcessDelayedActions();
            if (extPlaying) _updateLastStep = 5;
            DrainIncomingCommands(8);
            PumpResponseTextQueue();
            if (extPlaying) _updateLastStep = 6;
            UpdateFacePresetProbe();
            TickScenarioTextSend(now);
            if (extPlaying) _updateLastStep = 7;
        }

        private bool IsReloadKeyDown()
        {
            KeyboardShortcut reloadKey = ResolveReloadKey();
            return reloadKey.IsDown();
        }

        private void BindConfigEntries()
        {
            _cfgEnabled = Config.Bind(
                "General",
                "Enabled",
                true,
                "プラグイン機能を有効化する。");

            _cfgVerboseLog = Config.Bind(
                "General",
                "VerboseLog",
                true,
                "詳細ログ（Info）を有効化する。OFF時はWarning/Errorのみ出力。");

            _cfgReloadKey = Config.Bind(
                "Input",
                "ReloadKey",
                new KeyboardShortcut(KeyCode.R, KeyCode.LeftControl),
                "設定再読込キー。");

            _cfgStateDumpKey = Config.Bind(
                "Input",
                "StateDumpKey",
                new KeyboardShortcut(KeyCode.F8, KeyCode.LeftControl, KeyCode.LeftShift),
                "状態ダンプを専用ログへ1回だけ出すキー。");

            _cfgPlaybackVolume = Config.Bind(
                "Audio",
                "PlaybackVolume",
                1.0f,
                new ConfigDescription(
                    "外部音声の基本音量（0.0 - 1.0）。",
                    new AcceptableValueRange<float>(0f, 1f)));

            _cfgFemalePlaybackVolume = Config.Bind(
                "Audio",
                "FemalePlaybackVolume",
                1.0f,
                new ConfigDescription(
                    "女の子読み上げ時の音量（-1でPlaybackVolumeを使用）。",
                    new AcceptableValueRange<float>(-1f, 1f)));

            _cfgExternalPlaybackPitch = Config.Bind(
                "Audio",
                "ExternalPlaybackPitch",
                1.0f,
                new ConfigDescription(
                    "外部音声再生ピッチ（0.1 - 3.0）。",
                    new AcceptableValueRange<float>(0.1f, 3f)));

            const string cdSec = "ClothesDetection";
            _cfgTopKeywords      = Config.Bind(cdSec, "TopKeywords",       "上着,ジャケット,トップス",                          "トップス系ワード（カンマ区切り）");
            _cfgBottomKeywords   = Config.Bind(cdSec, "BottomKeywords",    "スカート,ホットパンツ,ミニスカ,ボトムス,ズボン,パンツ", "ボトムス系ワード");
            _cfgBraKeywords      = Config.Bind(cdSec, "BraKeywords",       "ブラ",                                               "ブラ系ワード");
            _cfgShortsKeywords   = Config.Bind(cdSec, "ShortsKeywords",    "パンティー,パンティ",                                 "パンティ系ワード");
            _cfgGlovesKeywords   = Config.Bind(cdSec, "GlovesKeywords",    "グローブ,手袋",                                      "手袋系ワード");
            _cfgPanthoseKeywords = Config.Bind(cdSec, "PanthoseKeywords",  "ガーターベルト,パンスト,ガーター",                    "パンスト系ワード");
            _cfgSocksKeywords    = Config.Bind(cdSec, "SocksKeywords",     "ストッキング,ニーハイ,靴下",                          "靴下系ワード");
            _cfgShoesKeywords    = Config.Bind(cdSec, "ShoesKeywords",     "ハイヒール,スニーカー,サンダル,ヒール,靴",            "靴系ワード");
            _cfgGlassesKeywords  = Config.Bind(cdSec, "GlassesKeywords",   "メガネ,眼鏡,めがね",                                   "眼鏡系ワード");
            _cfgRemoveKeywords   = Config.Bind(cdSec, "RemoveKeywords",    "脱ぐね,脱いじゃう",                                  "脱衣トリガーワード");
            _cfgShiftKeywords    = Config.Bind(cdSec, "ShiftKeywords",     "ずらすね,半脱ぎにするね",                             "ずらしトリガーワード");
            _cfgPutOnKeywords    = Config.Bind(cdSec, "PutOnKeywords",     "着るね,付けるね",                                    "着用トリガーワード");
            _cfgRemoveAllKeywords= Config.Bind(cdSec, "RemoveAllKeywords", "全裸になるね,全部脱ぐね,全部脱いじゃう",              "全脱ぎトリガーワード");
            _cfgPutOnAllKeywords = Config.Bind(cdSec, "PutOnAllKeywords",  "全部着るね",                                         "全着用トリガーワード");
            _cfgCoordPattern     = Config.Bind(cdSec, "CoordPattern",      "に着替えるね",                                       "着替えトリガーパターン");
            _cfgCameraTriggerKeywords = Config.Bind(cdSec, "CameraTriggerKeywords", "カメラにして,カメラ切り替えて,視点にして,視点切り替えて,アングルにして,で見せて", "カメラ切替トリガーワード");
            _cfgEnableVideoPlaybackByResponseText = Config.Bind(
                "VideoPlayback",
                "EnableVideoPlaybackByResponseText",
                true,
                "response_text解析で動画再生（\"流す\"など）を有効化する。");
            _cfgVideoPlaybackTriggerKeywords = Config.Bind(
                "VideoPlayback",
                "VideoPlaybackTriggerKeywords",
                "流す",
                "動画再生トリガーワード（カンマ区切り）。");
            _cfgSequenceSubtitleEnabled = Config.Bind(
                "SequenceSubtitle",
                "Enabled",
                true,
                "speak_sequence再生時に字幕をSubtitleEventBridgeへ送信する。");
            _cfgSequenceSubtitleHost = Config.Bind(
                "SequenceSubtitle",
                "Host",
                "127.0.0.1",
                "SubtitleEventBridgeのHTTPホスト。");
            _cfgSequenceSubtitlePort = Config.Bind(
                "SequenceSubtitle",
                "Port",
                18766,
                new ConfigDescription(
                    "SubtitleEventBridgeのHTTPポート。",
                    new AcceptableValueRange<int>(1, 65535)));
            _cfgSequenceSubtitleEndpointPath = Config.Bind(
                "SequenceSubtitle",
                "EndpointPath",
                "/subtitle-event",
                "SubtitleEventBridgeのエンドポイント。");
            _cfgSequenceSubtitleDisplayMode = Config.Bind(
                "SequenceSubtitle",
                "DisplayMode",
                "StackFemale",
                "送信する字幕display_mode。");
            _cfgSequenceSubtitleSendMode = Config.Bind(
                "SequenceSubtitle",
                "SendMode",
                "PerLine",
                new ConfigDescription(
                    "sequence字幕の送信方式。PerLine=行ごと、FullTextOnce=全文を最初に1回送信。",
                    new AcceptableValueList<string>("PerLine", "FullTextOnce")));
            _cfgSequenceSubtitleHoldPaddingSeconds = Config.Bind(
                "SequenceSubtitle",
                "HoldPaddingSeconds",
                0.2f,
                new ConfigDescription(
                    "各行字幕の音声長に足す保持秒数。",
                    new AcceptableValueRange<float>(0f, 5f)));
            _cfgSequenceSubtitleProgressPrefixEnabled = Config.Bind(
                "SequenceSubtitle",
                "ProgressPrefixEnabled",
                true,
                "複数行のspeak_sequence字幕にインデックス表示を付ける。");
            _cfgEnableFacePresetApply = Config.Bind(
                "FacePreset",
                "EnableFacePresetApply",
                true,
                "受信したfacePreset指定（name/id/random）で表情プリセットを適用する。");
            _cfgFacePresetJsonRelativePath = Config.Bind(
                "FacePreset",
                "FacePresetJsonRelativePath",
                DefaultFacePresetJsonRelativePath,
                "表情プリセットJSONの相対パス（DLL配置フォルダ基準）。");

            BindScenarioTextConfigEntries();

            RegisterConfigEntryEvents();
        }

        private void RegisterConfigEntryEvents()
        {
            HookConfigEntryEvent(_cfgEnabled, restartPipe: true);
            HookConfigEntryEvent(_cfgVerboseLog, restartPipe: false);
            HookConfigEntryEvent(_cfgReloadKey, restartPipe: false);
            HookConfigEntryEvent(_cfgStateDumpKey, restartPipe: false);
            HookConfigEntryEvent(_cfgPlaybackVolume, restartPipe: false);
            HookConfigEntryEvent(_cfgFemalePlaybackVolume, restartPipe: false);
            HookConfigEntryEvent(_cfgExternalPlaybackPitch, restartPipe: false);
            HookConfigEntryEvent(_cfgTopKeywords, restartPipe: false);
            HookConfigEntryEvent(_cfgBottomKeywords, restartPipe: false);
            HookConfigEntryEvent(_cfgBraKeywords, restartPipe: false);
            HookConfigEntryEvent(_cfgShortsKeywords, restartPipe: false);
            HookConfigEntryEvent(_cfgGlovesKeywords, restartPipe: false);
            HookConfigEntryEvent(_cfgPanthoseKeywords, restartPipe: false);
            HookConfigEntryEvent(_cfgSocksKeywords, restartPipe: false);
            HookConfigEntryEvent(_cfgShoesKeywords, restartPipe: false);
            HookConfigEntryEvent(_cfgGlassesKeywords, restartPipe: false);
            HookConfigEntryEvent(_cfgRemoveKeywords, restartPipe: false);
            HookConfigEntryEvent(_cfgShiftKeywords, restartPipe: false);
            HookConfigEntryEvent(_cfgPutOnKeywords, restartPipe: false);
            HookConfigEntryEvent(_cfgRemoveAllKeywords, restartPipe: false);
            HookConfigEntryEvent(_cfgPutOnAllKeywords, restartPipe: false);
            HookConfigEntryEvent(_cfgCoordPattern, restartPipe: false);
            HookConfigEntryEvent(_cfgEnableVideoPlaybackByResponseText, restartPipe: false);
            HookConfigEntryEvent(_cfgVideoPlaybackTriggerKeywords, restartPipe: false);
            HookConfigEntryEvent(_cfgSequenceSubtitleEnabled, restartPipe: false);
            HookConfigEntryEvent(_cfgSequenceSubtitleHost, restartPipe: false);
            HookConfigEntryEvent(_cfgSequenceSubtitlePort, restartPipe: false);
            HookConfigEntryEvent(_cfgSequenceSubtitleEndpointPath, restartPipe: false);
            HookConfigEntryEvent(_cfgSequenceSubtitleDisplayMode, restartPipe: false);
            HookConfigEntryEvent(_cfgSequenceSubtitleSendMode, restartPipe: false);
            HookConfigEntryEvent(_cfgSequenceSubtitleHoldPaddingSeconds, restartPipe: false);
            HookConfigEntryEvent(_cfgSequenceSubtitleProgressPrefixEnabled, restartPipe: false);
            HookConfigEntryEvent(_cfgEnableFacePresetApply, restartPipe: false);
            HookConfigEntryEvent(_cfgFacePresetJsonRelativePath, restartPipe: false);
            RegisterScenarioTextConfigEntryEvents();
        }

        private void EnsurePoseControlConfigEntries()
        {
            EnsurePoseGlobalConfigEntry();
            EnsurePoseCategoryConfigEntries();
            EnsurePoseRuleConfigEntries();
            EnsurePoseInferRuleConfigEntries();
            UpdatePoseControlReadOnlyState();
        }

        private void EnsurePoseGlobalConfigEntry()
        {
            if (_cfgPoseChangeEnabled != null)
            {
                EnsurePoseSimpleModeConfigEntries();
                return;
            }

            _cfgPoseChangeEnabled = Config.Bind(
                PoseControlSectionName,
                "【最上位】体位変更を有効化",
                _poseChangeEnabled,
                BuildPoseControlConfigDescription("このチェックがOFFの間は、体位変更の全項目が無効（グレーアウト）になります。", order: 1100, readOnly: false, isAdvanced: false));
            RegisterPoseReadonlyAttribute(_cfgPoseChangeEnabled);
            _cfgPoseChangeEnabled.SettingChanged += (_, __) =>
            {
                if (_suppressPoseConfigChangeEvent)
                {
                    return;
                }

                _poseChangeEnabled = _cfgPoseChangeEnabled != null && _cfgPoseChangeEnabled.Value;
                if (_poseChangeEnabled)
                {
                    ExpandPoseSectionsOnEnable();
                }
                UpdatePoseControlReadOnlyState();
                RefreshConfigurationManagerSettingList("pose-global-toggle");
                SaveCurrentPoseScoreRulesToFile("config-manager:pose-global");
            };

            EnsurePoseSimpleModeConfigEntries();
        }

        private void EnsurePoseSimpleModeConfigEntries()
        {
            _poseSimpleModeTriggerKeywords = NormalizePoseSimpleModeTriggerKeywords(_poseSimpleModeTriggerKeywords);

            if (_cfgPoseSimpleModeEnabled == null)
            {
                _cfgPoseSimpleModeEnabled = Config.Bind(
                    PoseControlSectionName,
                    "【簡易】シンプル体位モード",
                    _poseSimpleModeEnabled,
                    BuildPoseControlConfigDescription("ONで「体位名 + になるね」の同一行判定のみ使用（複雑ルール/推定は完全バイパス）。", order: 1090, readOnly: !_poseChangeEnabled, isAdvanced: false));
                RegisterPoseReadonlyAttribute(_cfgPoseSimpleModeEnabled);
                _cfgPoseSimpleModeEnabled.SettingChanged += (_, __) =>
                {
                    if (_suppressPoseConfigChangeEvent)
                    {
                        return;
                    }

                    _poseSimpleModeEnabled = _cfgPoseSimpleModeEnabled != null && _cfgPoseSimpleModeEnabled.Value;
                    UpdatePoseControlReadOnlyState();
                    RefreshConfigurationManagerSettingList("pose-simple-mode-toggle");
                    SaveCurrentPoseScoreRulesToFile("config-manager:pose-simple-mode");
                };
            }

            if (_cfgPoseSimpleModeTriggerKeywords == null)
            {
                _cfgPoseSimpleModeTriggerKeywords = Config.Bind(
                    PoseControlSectionName,
                    "【簡易】体位変更キーワード",
                    _poseSimpleModeTriggerKeywords,
                    BuildPoseControlConfigDescription("シンプル体位モードの発火語（カンマ区切り）。同じ行に体位名とこの語があると変更。", order: 1085, readOnly: !_poseChangeEnabled, isAdvanced: false));
                RegisterPoseReadonlyAttribute(_cfgPoseSimpleModeTriggerKeywords);
                _cfgPoseSimpleModeTriggerKeywords.SettingChanged += (_, __) =>
                {
                    if (_suppressPoseConfigChangeEvent)
                    {
                        return;
                    }

                    _poseSimpleModeTriggerKeywords = NormalizePoseSimpleModeTriggerKeywords(_cfgPoseSimpleModeTriggerKeywords.Value);
                    if (_cfgPoseSimpleModeTriggerKeywords != null && !string.Equals(_cfgPoseSimpleModeTriggerKeywords.Value, _poseSimpleModeTriggerKeywords, StringComparison.Ordinal))
                    {
                        bool previousSuppress = _suppressPoseConfigChangeEvent;
                        _suppressPoseConfigChangeEvent = true;
                        try
                        {
                            _cfgPoseSimpleModeTriggerKeywords.Value = _poseSimpleModeTriggerKeywords;
                        }
                        finally
                        {
                            _suppressPoseConfigChangeEvent = previousSuppress;
                        }
                    }
                    UpdatePoseControlReadOnlyState();
                    RefreshConfigurationManagerSettingList("pose-simple-trigger-toggle");
                    SaveCurrentPoseScoreRulesToFile("config-manager:pose-simple-trigger");
                };
            }
        }

        private void EnsurePoseCategoryConfigEntries()
        {
            EnsurePoseCategorySectionControlEntries();

            var categories = _poseCategoryEnabled.Keys
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();

            int order = 900;
            foreach (string category in categories)
            {
                if (string.IsNullOrWhiteSpace(category))
                {
                    continue;
                }

                if (_cfgPoseCategoryEnabledEntries.ContainsKey(category))
                {
                    order--;
                    continue;
                }

                bool enabled = IsPoseCategoryEnabled(category);
                var entry = Config.Bind(
                    PoseControlCategoriesSectionName,
                    category,
                    enabled,
                    BuildPoseControlConfigDescription($"カテゴリ '{category}' の有効/無効。", order, readOnly: !_poseChangeEnabled));
                _cfgPoseCategoryEnabledEntries[category] = entry;
                RegisterPoseReadonlyAttribute(entry);
                entry.SettingChanged += (_, __) =>
                {
                    if (_suppressPoseConfigChangeEvent)
                    {
                        return;
                    }

                    _poseCategoryEnabled[category] = entry.Value;
                    RefreshConfigurationManagerSettingList("pose-category-toggle");
                    SaveCurrentPoseScoreRulesToFile("config-manager:pose-category");
                };
                order--;
            }
        }

        private void EnsurePoseCategorySectionControlEntries()
        {
            if (_cfgPoseCategoriesExpanded == null)
            {
                _cfgPoseCategoriesExpanded = Config.Bind(
                    PoseControlCategoriesSectionName,
                    "【表示】カテゴリ一覧",
                    _poseCategoriesExpanded,
                    BuildPoseControlToggleButtonDescription(
                        openLabel: "カテゴリ一覧を開く",
                        closeLabel: "カテゴリ一覧を閉じる",
                        order: 990,
                        readOnly: !_poseChangeEnabled));
                RegisterPoseReadonlyAttribute(_cfgPoseCategoriesExpanded);
                _cfgPoseCategoriesExpanded.SettingChanged += (_, __) =>
                {
                    if (_suppressPoseConfigChangeEvent)
                    {
                        return;
                    }

                    _poseCategoriesExpanded = _cfgPoseCategoriesExpanded != null && _cfgPoseCategoriesExpanded.Value;
                    UpdatePoseControlReadOnlyState();
                    RefreshConfigurationManagerSettingList("pose-category-expand-toggle");
                };
            }
            _poseCategoriesExpanded = _cfgPoseCategoriesExpanded != null && _cfgPoseCategoriesExpanded.Value;
        }

        private void EnsurePoseRuleConfigEntries()
        {
            EnsurePoseRuleSectionControlEntries();

            var rules = (_poseKeywordScoreRules ?? new List<PoseKeywordScoreRule>())
                .Where(r => r != null && !string.IsNullOrWhiteSpace(r.RuleId))
                .OrderByDescending(r => r.Priority)
                .ThenBy(r => r.RuleId, StringComparer.Ordinal)
                .ToArray();

            int order = 800;
            int index = 1;
            foreach (PoseKeywordScoreRule rule in rules)
            {
                if (_cfgPoseRuleEnabledEntries.ContainsKey(rule.RuleId))
                {
                    order--;
                    index++;
                    continue;
                }

                bool enabled = rule.Enabled != false;
                string key = BuildPoseRuleEntryLabel(rule, index);
                string ruleId = rule.RuleId;
                var entry = Config.Bind(
                    PoseControlRulesSectionName,
                    key,
                    enabled,
                    BuildPoseControlConfigDescription($"[{rule.Category}] ID={ruleId}", order, readOnly: !_poseChangeEnabled || !_poseRulesEnabled));
                _cfgPoseRuleEnabledEntries[ruleId] = entry;
                RegisterPoseReadonlyAttribute(entry);
                entry.SettingChanged += (_, __) =>
                {
                    if (_suppressPoseConfigChangeEvent)
                    {
                        return;
                    }

                    PoseKeywordScoreRule target = _poseKeywordScoreRules.FirstOrDefault(x => x != null && string.Equals(x.RuleId, ruleId, StringComparison.Ordinal));
                    if (target != null)
                    {
                        target.Enabled = entry.Value;
                    }
                    RefreshConfigurationManagerSettingList("pose-rule-toggle");
                    SaveCurrentPoseScoreRulesToFile("config-manager:pose-rule");
                };
                order--;
                index++;
            }
        }

        private void EnsurePoseRuleSectionControlEntries()
        {
            if (_cfgPoseRulesEnabled == null)
            {
                _cfgPoseRulesEnabled = Config.Bind(
                    PoseControlRulesSectionName,
                    "【全体】ルールを有効化",
                    _poseRulesEnabled,
                    BuildPoseControlConfigDescription("体位ルール一覧をまとめてON/OFFします。", order: 1000, readOnly: false, isAdvanced: false));
                RegisterPoseReadonlyAttribute(_cfgPoseRulesEnabled);
                _cfgPoseRulesEnabled.SettingChanged += (_, __) =>
                {
                    if (_suppressPoseConfigChangeEvent)
                    {
                        return;
                    }

                    _poseRulesEnabled = _cfgPoseRulesEnabled != null && _cfgPoseRulesEnabled.Value;
                    if (_poseRulesEnabled && !_poseRulesExpanded)
                    {
                        SetPoseSectionExpanded(ref _poseRulesExpanded, _cfgPoseRulesExpanded, true);
                    }
                    UpdatePoseControlReadOnlyState();
                    RefreshConfigurationManagerSettingList("pose-rules-global-toggle");
                    SaveCurrentPoseScoreRulesToFile("config-manager:pose-rules-global");
                };
            }

            if (_cfgPoseRulesExpanded == null)
            {
                _cfgPoseRulesExpanded = Config.Bind(
                    PoseControlRulesSectionName,
                    "【表示】ルール一覧",
                    _poseRulesExpanded,
                    BuildPoseControlToggleButtonDescription(
                        openLabel: "ルール一覧を開く",
                        closeLabel: "ルール一覧を閉じる",
                        order: 990,
                        readOnly: !_poseChangeEnabled || !_poseRulesEnabled));
                RegisterPoseReadonlyAttribute(_cfgPoseRulesExpanded);
                _cfgPoseRulesExpanded.SettingChanged += (_, __) =>
                {
                    if (_suppressPoseConfigChangeEvent)
                    {
                        return;
                    }

                    _poseRulesExpanded = _cfgPoseRulesExpanded != null && _cfgPoseRulesExpanded.Value;
                    UpdatePoseControlReadOnlyState();
                    RefreshConfigurationManagerSettingList("pose-rules-expand-toggle");
                };
            }
            _poseRulesExpanded = _cfgPoseRulesExpanded != null && _cfgPoseRulesExpanded.Value;
        }

        private void EnsurePoseInferRuleConfigEntries()
        {
            EnsurePoseInferSectionControlEntries();

            var rules = (_poseCategoryInferRules ?? new List<PoseCategoryInferRule>())
                .Where(r => r != null && !string.IsNullOrWhiteSpace(r.RuleId))
                .OrderByDescending(r => r.Priority)
                .ThenBy(r => r.RuleId, StringComparer.Ordinal)
                .ToArray();

            int order = 700;
            int index = 1;
            foreach (PoseCategoryInferRule rule in rules)
            {
                if (_cfgPoseInferRuleEnabledEntries.ContainsKey(rule.RuleId))
                {
                    order--;
                    index++;
                    continue;
                }

                bool enabled = rule.Enabled != false;
                string key = BuildPoseInferRuleEntryLabel(rule, index);
                string ruleId = rule.RuleId;
                var entry = Config.Bind(
                    PoseControlInferRulesSectionName,
                    key,
                    enabled,
                    BuildPoseControlConfigDescription($"[{rule.TargetCategory}] ID={ruleId}", order, readOnly: !_poseChangeEnabled || !_poseInferRulesEnabled));
                _cfgPoseInferRuleEnabledEntries[ruleId] = entry;
                RegisterPoseReadonlyAttribute(entry);
                entry.SettingChanged += (_, __) =>
                {
                    if (_suppressPoseConfigChangeEvent)
                    {
                        return;
                    }

                    PoseCategoryInferRule target = _poseCategoryInferRules.FirstOrDefault(x => x != null && string.Equals(x.RuleId, ruleId, StringComparison.Ordinal));
                    if (target != null)
                    {
                        target.Enabled = entry.Value;
                    }
                    RefreshConfigurationManagerSettingList("pose-infer-toggle");
                    SaveCurrentPoseScoreRulesToFile("config-manager:pose-infer-rule");
                };
                order--;
                index++;
            }
        }

        private void EnsurePoseInferSectionControlEntries()
        {
            if (_cfgPoseInferRulesEnabled == null)
            {
                _cfgPoseInferRulesEnabled = Config.Bind(
                    PoseControlInferRulesSectionName,
                    "【全体】推定ルールを有効化",
                    _poseInferRulesEnabled,
                    BuildPoseControlConfigDescription("体位推定ルール一覧をまとめてON/OFFします。", order: 1000, readOnly: false, isAdvanced: false));
                RegisterPoseReadonlyAttribute(_cfgPoseInferRulesEnabled);
                _cfgPoseInferRulesEnabled.SettingChanged += (_, __) =>
                {
                    if (_suppressPoseConfigChangeEvent)
                    {
                        return;
                    }

                    _poseInferRulesEnabled = _cfgPoseInferRulesEnabled != null && _cfgPoseInferRulesEnabled.Value;
                    if (_poseInferRulesEnabled && !_poseInferRulesExpanded)
                    {
                        SetPoseSectionExpanded(ref _poseInferRulesExpanded, _cfgPoseInferRulesExpanded, true);
                    }
                    UpdatePoseControlReadOnlyState();
                    RefreshConfigurationManagerSettingList("pose-infer-global-toggle");
                    SaveCurrentPoseScoreRulesToFile("config-manager:pose-infer-global");
                };
            }

            if (_cfgPoseInferRulesExpanded == null)
            {
                _cfgPoseInferRulesExpanded = Config.Bind(
                    PoseControlInferRulesSectionName,
                    "【表示】推定ルール一覧",
                    _poseInferRulesExpanded,
                    BuildPoseControlToggleButtonDescription(
                        openLabel: "推定ルール一覧を開く",
                        closeLabel: "推定ルール一覧を閉じる",
                        order: 990,
                        readOnly: !_poseChangeEnabled || !_poseInferRulesEnabled));
                RegisterPoseReadonlyAttribute(_cfgPoseInferRulesExpanded);
                _cfgPoseInferRulesExpanded.SettingChanged += (_, __) =>
                {
                    if (_suppressPoseConfigChangeEvent)
                    {
                        return;
                    }

                    _poseInferRulesExpanded = _cfgPoseInferRulesExpanded != null && _cfgPoseInferRulesExpanded.Value;
                    UpdatePoseControlReadOnlyState();
                    RefreshConfigurationManagerSettingList("pose-infer-expand-toggle");
                };
            }
            _poseInferRulesExpanded = _cfgPoseInferRulesExpanded != null && _cfgPoseInferRulesExpanded.Value;
        }

        private ConfigDescription BuildPoseControlToggleButtonDescription(string openLabel, string closeLabel, int order, bool readOnly)
        {
            var attrs = new ConfigurationManager.ConfigurationManagerAttributes
            {
                Order = order,
                ReadOnly = readOnly,
                HideSettingName = true,
                HideDefaultButton = true
            };
            attrs.CustomDrawer = entryBase =>
            {
                var boolEntry = entryBase as ConfigEntry<bool>;
                if (boolEntry == null)
                {
                    return;
                }

                bool isOpen = boolEntry.Value;
                string buttonText = isOpen ? closeLabel : openLabel;
                bool prevEnabled = GUI.enabled;
                if (attrs.ReadOnly == true)
                {
                    GUI.enabled = false;
                }

                if (GUILayout.Button(buttonText, GUILayout.ExpandWidth(true)))
                {
                    boolEntry.Value = !boolEntry.Value;
                }

                GUI.enabled = prevEnabled;
            };
            return new ConfigDescription(string.Empty, null, attrs);
        }

        private ConfigDescription BuildPoseControlConfigDescription(string description, int order, bool readOnly, bool? browsable = null, bool? isAdvanced = null)
        {
            var attrs = new ConfigurationManager.ConfigurationManagerAttributes
            {
                Order = order,
                ReadOnly = readOnly
            };
            if (browsable.HasValue)
            {
                attrs.Browsable = browsable.Value;
            }
            if (isAdvanced.HasValue)
            {
                attrs.IsAdvanced = isAdvanced.Value;
            }
            return new ConfigDescription(description, null, attrs);
        }

        private void RegisterPoseReadonlyAttribute(ConfigEntryBase entryBase)
        {
            if (entryBase == null)
            {
                return;
            }

            var tags = entryBase.Description != null ? entryBase.Description.Tags : null;
            if (tags == null)
            {
                return;
            }

            foreach (object tag in tags)
            {
                var attr = tag as ConfigurationManager.ConfigurationManagerAttributes;
                if (attr != null)
                {
                    _cfgPoseReadonlyAttributes[entryBase] = attr;
                    break;
                }
            }
        }

        private void UpdatePoseControlReadOnlyState()
        {
            bool showCategories = _poseCategoriesExpanded;
            bool showRules = _poseRulesExpanded && !_poseSimpleModeEnabled;
            bool showInferRules = _poseInferRulesExpanded && !_poseSimpleModeEnabled;

            foreach (var pair in _cfgPoseReadonlyAttributes)
            {
                if (pair.Key == null || pair.Value == null)
                {
                    continue;
                }

                // グローバルトグルは常に編集可能
                if (_cfgPoseChangeEnabled != null && ReferenceEquals(pair.Key, _cfgPoseChangeEnabled))
                {
                    pair.Value.ReadOnly = false;
                    pair.Value.Browsable = true;
                    continue;
                }
                if (_cfgPoseCategoriesExpanded != null && ReferenceEquals(pair.Key, _cfgPoseCategoriesExpanded))
                {
                    pair.Value.ReadOnly = !_poseChangeEnabled;
                    pair.Value.Browsable = true;
                    continue;
                }
                if (_cfgPoseSimpleModeEnabled != null && ReferenceEquals(pair.Key, _cfgPoseSimpleModeEnabled))
                {
                    pair.Value.ReadOnly = !_poseChangeEnabled;
                    pair.Value.Browsable = true;
                    continue;
                }
                if (_cfgPoseSimpleModeTriggerKeywords != null && ReferenceEquals(pair.Key, _cfgPoseSimpleModeTriggerKeywords))
                {
                    pair.Value.ReadOnly = !_poseChangeEnabled;
                    pair.Value.Browsable = true;
                    continue;
                }
                if (_cfgPoseRulesEnabled != null && ReferenceEquals(pair.Key, _cfgPoseRulesEnabled))
                {
                    pair.Value.ReadOnly = !_poseChangeEnabled || _poseSimpleModeEnabled;
                    pair.Value.Browsable = !_poseSimpleModeEnabled;
                    continue;
                }
                if (_cfgPoseRulesExpanded != null && ReferenceEquals(pair.Key, _cfgPoseRulesExpanded))
                {
                    pair.Value.ReadOnly = !_poseChangeEnabled || !_poseRulesEnabled || _poseSimpleModeEnabled;
                    pair.Value.Browsable = !_poseSimpleModeEnabled;
                    continue;
                }
                if (_cfgPoseInferRulesEnabled != null && ReferenceEquals(pair.Key, _cfgPoseInferRulesEnabled))
                {
                    pair.Value.ReadOnly = !_poseChangeEnabled || _poseSimpleModeEnabled;
                    pair.Value.Browsable = !_poseSimpleModeEnabled;
                    continue;
                }
                if (_cfgPoseInferRulesExpanded != null && ReferenceEquals(pair.Key, _cfgPoseInferRulesExpanded))
                {
                    pair.Value.ReadOnly = !_poseChangeEnabled || !_poseInferRulesEnabled || _poseSimpleModeEnabled;
                    pair.Value.Browsable = !_poseSimpleModeEnabled;
                    continue;
                }

                bool isCategoryEntry = _cfgPoseCategoryEnabledEntries.Values.Any(v => ReferenceEquals(v, pair.Key));
                bool isRuleEntry = _cfgPoseRuleEnabledEntries.Values.Any(v => ReferenceEquals(v, pair.Key));
                bool isInferRuleEntry = _cfgPoseInferRuleEnabledEntries.Values.Any(v => ReferenceEquals(v, pair.Key));
                if (isCategoryEntry)
                {
                    pair.Value.ReadOnly = !_poseChangeEnabled;
                    pair.Value.Browsable = showCategories;
                    continue;
                }
                if (isRuleEntry)
                {
                    pair.Value.ReadOnly = !_poseChangeEnabled || !_poseRulesEnabled || _poseSimpleModeEnabled;
                    pair.Value.Browsable = showRules;
                    continue;
                }
                if (isInferRuleEntry)
                {
                    pair.Value.ReadOnly = !_poseChangeEnabled || !_poseInferRulesEnabled || _poseSimpleModeEnabled;
                    pair.Value.Browsable = showInferRules;
                    continue;
                }

                pair.Value.ReadOnly = !_poseChangeEnabled;
                pair.Value.Browsable = true;
            }
        }

        private void ExpandPoseSectionsOnEnable()
        {
            SetPoseSectionExpanded(ref _poseCategoriesExpanded, _cfgPoseCategoriesExpanded, true);
            SetPoseSectionExpanded(ref _poseRulesExpanded, _cfgPoseRulesExpanded, true);
            SetPoseSectionExpanded(ref _poseInferRulesExpanded, _cfgPoseInferRulesExpanded, true);
        }

        private void SetPoseSectionExpanded(ref bool stateField, ConfigEntry<bool> entry, bool value)
        {
            if (stateField == value && (entry == null || entry.Value == value))
            {
                return;
            }

            bool previousSuppress = _suppressPoseConfigChangeEvent;
            _suppressPoseConfigChangeEvent = true;
            try
            {
                stateField = value;
                if (entry != null)
                {
                    entry.Value = value;
                }
            }
            finally
            {
                _suppressPoseConfigChangeEvent = previousSuppress;
            }
        }

        private void RefreshConfigurationManagerSettingList(string reason)
        {
            try
            {
                if (_configurationManagerType == null)
                {
                    _configurationManagerType = Type.GetType("ConfigurationManager.ConfigurationManager, ConfigurationManager");
                    if (_configurationManagerType == null)
                    {
                        return;
                    }
                }

                if (_configurationManagerBuildSettingListMethod == null)
                {
                    _configurationManagerBuildSettingListMethod =
                        _configurationManagerType.GetMethod("BuildSettingList", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (_configurationManagerBuildSettingListMethod == null)
                    {
                        return;
                    }
                }

                UnityEngine.Object[] managers = UnityEngine.Object.FindObjectsOfType(_configurationManagerType);
                if (managers == null || managers.Length <= 0)
                {
                    return;
                }

                foreach (UnityEngine.Object manager in managers)
                {
                    _configurationManagerBuildSettingListMethod.Invoke(manager, null);
                }
            }
            catch (Exception ex)
            {
                LogWarn("[pose-cfgui] refresh failed reason=" + reason + " message=" + ex.Message);
            }
        }

        private void SyncPoseControlConfigEntriesFromRuntime()
        {
            _suppressPoseConfigChangeEvent = true;
            try
            {
                if (_cfgPoseChangeEnabled != null)
                {
                    _cfgPoseChangeEnabled.Value = _poseChangeEnabled;
                }
                if (_cfgPoseSimpleModeEnabled != null)
                {
                    _cfgPoseSimpleModeEnabled.Value = _poseSimpleModeEnabled;
                }
                if (_cfgPoseSimpleModeTriggerKeywords != null)
                {
                    _cfgPoseSimpleModeTriggerKeywords.Value = _poseSimpleModeTriggerKeywords;
                }
                if (_cfgPoseRulesEnabled != null)
                {
                    _cfgPoseRulesEnabled.Value = _poseRulesEnabled;
                }
                if (_cfgPoseInferRulesEnabled != null)
                {
                    _cfgPoseInferRulesEnabled.Value = _poseInferRulesEnabled;
                }
                foreach (var pair in _cfgPoseCategoryEnabledEntries)
                {
                    bool enabled = IsPoseCategoryEnabled(pair.Key);
                    if (pair.Value != null)
                    {
                        pair.Value.Value = enabled;
                    }
                }

                foreach (var pair in _cfgPoseRuleEnabledEntries)
                {
                    PoseKeywordScoreRule rule = _poseKeywordScoreRules.FirstOrDefault(r => r != null && string.Equals(r.RuleId, pair.Key, StringComparison.Ordinal));
                    bool enabled = rule == null || rule.Enabled != false;
                    if (pair.Value != null)
                    {
                        pair.Value.Value = enabled;
                    }
                }

                foreach (var pair in _cfgPoseInferRuleEnabledEntries)
                {
                    PoseCategoryInferRule rule = _poseCategoryInferRules.FirstOrDefault(r => r != null && string.Equals(r.RuleId, pair.Key, StringComparison.Ordinal));
                    bool enabled = rule == null || rule.Enabled != false;
                    if (pair.Value != null)
                    {
                        pair.Value.Value = enabled;
                    }
                }
            }
            finally
            {
                _suppressPoseConfigChangeEvent = false;
                UpdatePoseControlReadOnlyState();
            }
        }

        private static string NormalizePoseSimpleModeTriggerKeywords(string csv)
        {
            string source = string.IsNullOrWhiteSpace(csv) ? DefaultPoseSimpleModeTriggerKeywords : csv;
            source = source.Replace('、', ',');
            string[] keywords = SplitKeywords(source);
            if (keywords.Length <= 0)
            {
                keywords = SplitKeywords(DefaultPoseSimpleModeTriggerKeywords);
            }

            return string.Join(",", keywords
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .ToArray());
        }

        private string[] GetPoseSimpleModeTriggerKeywords()
        {
            string normalized = NormalizePoseSimpleModeTriggerKeywords(_poseSimpleModeTriggerKeywords);
            if (!string.Equals(_poseSimpleModeTriggerKeywords, normalized, StringComparison.Ordinal))
            {
                _poseSimpleModeTriggerKeywords = normalized;
            }

            return SplitKeywords(normalized);
        }

        private void HookConfigEntryEvent<T>(ConfigEntry<T> entry, bool restartPipe)
        {
            if (entry == null)
            {
                return;
            }

            string entryName = entry.Definition.Section + "/" + entry.Definition.Key;
            entry.SettingChanged += (_, __) => OnConfigEntryChanged(restartPipe, entryName, entry.BoxedValue);
        }

        private void OnConfigEntryChanged(bool restartPipe, string entryName, object currentValue)
        {
            if (_suppressConfigChangeEvent)
            {
                return;
            }

            string safeEntryName = string.IsNullOrWhiteSpace(entryName) ? "(unknown)" : entryName;
            string safeValue = currentValue == null ? "(null)" : currentValue.ToString();
            LogWarn("[cfg] SettingChanged entry=" + safeEntryName + " restartPipe=" + restartPipe + " value=" + safeValue);

            ApplyConfigEntryOverridesToSettings();
            SaveSettingsToConfigJson("config-manager");
            Settings = SettingsStore.LoadOrCreate(PluginDir, Log, LogWarn, LogError);
            ApplySettingsToConfigEntries(Settings);
            SaveConfigFile(reason: "config-manager-sync");
            Log("[cfg] change applied and persisted to config.json");
            if (restartPipe)
            {
                StartOrRestartPipeServer(forceRestart: false, reason: "config_changed:" + safeEntryName);
            }
        }

        private void ApplySettingsToConfigEntries(PluginSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            _suppressConfigChangeEvent = true;
            try
            {
                if (_cfgEnabled != null) _cfgEnabled.Value = settings.Enabled;
                if (_cfgVerboseLog != null) _cfgVerboseLog.Value = settings.VerboseLog;
                if (_cfgReloadKey != null) _cfgReloadKey.Value = ParseKeyboardShortcut(settings.ReloadKey, _cfgReloadKey.Value);
                if (_cfgStateDumpKey != null) _cfgStateDumpKey.Value = ParseKeyboardShortcut(settings.StateDumpKey, _cfgStateDumpKey.Value);
                if (_cfgPlaybackVolume != null) _cfgPlaybackVolume.Value = Mathf.Clamp01(settings.PlaybackVolume);
                if (_cfgFemalePlaybackVolume != null) _cfgFemalePlaybackVolume.Value = Mathf.Clamp(settings.FemalePlaybackVolume, -1f, 1f);
                if (_cfgExternalPlaybackPitch != null) _cfgExternalPlaybackPitch.Value = Mathf.Clamp(settings.ExternalPlaybackPitch, 0.1f, 3f);

                if (_cfgTopKeywords != null) _cfgTopKeywords.Value = settings.TopKeywords ?? _cfgTopKeywords.Value;
                if (_cfgBottomKeywords != null) _cfgBottomKeywords.Value = settings.BottomKeywords ?? _cfgBottomKeywords.Value;
                if (_cfgBraKeywords != null) _cfgBraKeywords.Value = settings.BraKeywords ?? _cfgBraKeywords.Value;
                if (_cfgShortsKeywords != null) _cfgShortsKeywords.Value = settings.ShortsKeywords ?? _cfgShortsKeywords.Value;
                if (_cfgGlovesKeywords != null) _cfgGlovesKeywords.Value = settings.GlovesKeywords ?? _cfgGlovesKeywords.Value;
                if (_cfgPanthoseKeywords != null) _cfgPanthoseKeywords.Value = settings.PanthoseKeywords ?? _cfgPanthoseKeywords.Value;
                if (_cfgSocksKeywords != null) _cfgSocksKeywords.Value = settings.SocksKeywords ?? _cfgSocksKeywords.Value;
                if (_cfgShoesKeywords != null) _cfgShoesKeywords.Value = settings.ShoesKeywords ?? _cfgShoesKeywords.Value;
                if (_cfgGlassesKeywords != null) _cfgGlassesKeywords.Value = settings.GlassesKeywords ?? _cfgGlassesKeywords.Value;
                if (_cfgRemoveKeywords != null) _cfgRemoveKeywords.Value = settings.RemoveKeywords ?? _cfgRemoveKeywords.Value;
                if (_cfgShiftKeywords != null) _cfgShiftKeywords.Value = settings.ShiftKeywords ?? _cfgShiftKeywords.Value;
                if (_cfgPutOnKeywords != null) _cfgPutOnKeywords.Value = settings.PutOnKeywords ?? _cfgPutOnKeywords.Value;
                if (_cfgRemoveAllKeywords != null) _cfgRemoveAllKeywords.Value = settings.RemoveAllKeywords ?? _cfgRemoveAllKeywords.Value;
                if (_cfgPutOnAllKeywords != null) _cfgPutOnAllKeywords.Value = settings.PutOnAllKeywords ?? _cfgPutOnAllKeywords.Value;
                if (_cfgCoordPattern != null) _cfgCoordPattern.Value = settings.CoordPattern ?? _cfgCoordPattern.Value;
                if (_cfgCameraTriggerKeywords != null) _cfgCameraTriggerKeywords.Value = string.IsNullOrWhiteSpace(settings.CameraTriggerKeywords) ? "カメラにして,カメラ切り替えて,視点にして,視点切り替えて,アングルにして,で見せて" : settings.CameraTriggerKeywords.Trim();
                if (_cfgEnableVideoPlaybackByResponseText != null) _cfgEnableVideoPlaybackByResponseText.Value = settings.EnableVideoPlaybackByResponseText;
                if (_cfgVideoPlaybackTriggerKeywords != null)
                {
                    _cfgVideoPlaybackTriggerKeywords.Value = string.IsNullOrWhiteSpace(settings.VideoPlaybackTriggerKeywords)
                        ? "流す"
                        : settings.VideoPlaybackTriggerKeywords.Trim();
                }
                if (_cfgSequenceSubtitleEnabled != null) _cfgSequenceSubtitleEnabled.Value = settings.SequenceSubtitleEnabled;
                if (_cfgSequenceSubtitleHost != null) _cfgSequenceSubtitleHost.Value = string.IsNullOrWhiteSpace(settings.SequenceSubtitleHost) ? "127.0.0.1" : settings.SequenceSubtitleHost.Trim();
                if (_cfgSequenceSubtitlePort != null) _cfgSequenceSubtitlePort.Value = Mathf.Clamp(settings.SequenceSubtitlePort, 1, 65535);
                if (_cfgSequenceSubtitleEndpointPath != null) _cfgSequenceSubtitleEndpointPath.Value = string.IsNullOrWhiteSpace(settings.SequenceSubtitleEndpointPath) ? "/subtitle-event" : settings.SequenceSubtitleEndpointPath.Trim();
                if (_cfgSequenceSubtitleDisplayMode != null) _cfgSequenceSubtitleDisplayMode.Value = string.IsNullOrWhiteSpace(settings.SequenceSubtitleDisplayMode) ? "StackFemale" : settings.SequenceSubtitleDisplayMode.Trim();
                if (_cfgSequenceSubtitleSendMode != null) _cfgSequenceSubtitleSendMode.Value = NormalizeSequenceSubtitleSendMode(settings.SequenceSubtitleSendMode);
                if (_cfgSequenceSubtitleHoldPaddingSeconds != null) _cfgSequenceSubtitleHoldPaddingSeconds.Value = Mathf.Clamp(settings.SequenceSubtitleHoldPaddingSeconds, 0f, 5f);
                if (_cfgSequenceSubtitleProgressPrefixEnabled != null) _cfgSequenceSubtitleProgressPrefixEnabled.Value = settings.SequenceSubtitleProgressPrefixEnabled;
                if (_cfgEnableFacePresetApply != null) _cfgEnableFacePresetApply.Value = settings.EnableFacePresetApply;
                if (_cfgFacePresetJsonRelativePath != null)
                {
                    _cfgFacePresetJsonRelativePath.Value = string.IsNullOrWhiteSpace(settings.FacePresetJsonRelativePath)
                        ? DefaultFacePresetJsonRelativePath
                        : settings.FacePresetJsonRelativePath.Trim();
                }

                ApplyScenarioTextSettingsToConfigEntries(settings);
            }
            finally
            {
                _suppressConfigChangeEvent = false;
            }
        }

        private void ApplyConfigEntryOverridesToSettings()
        {
            if (Settings == null)
            {
                return;
            }

            Settings.Enabled = _cfgEnabled != null ? _cfgEnabled.Value : Settings.Enabled;
            Settings.VerboseLog = _cfgVerboseLog != null ? _cfgVerboseLog.Value : Settings.VerboseLog;
            Settings.ReloadKey = ResolveReloadKey().ToString();
            Settings.StateDumpKey = ResolveStateDumpKey().ToString();
            Settings.PlaybackVolume = _cfgPlaybackVolume != null ? Mathf.Clamp01(_cfgPlaybackVolume.Value) : Settings.PlaybackVolume;
            Settings.FemalePlaybackVolume = _cfgFemalePlaybackVolume != null
                ? Mathf.Clamp(_cfgFemalePlaybackVolume.Value, -1f, 1f)
                : Settings.FemalePlaybackVolume;
            Settings.ExternalPlaybackPitch = _cfgExternalPlaybackPitch != null
                ? Mathf.Clamp(_cfgExternalPlaybackPitch.Value, 0.1f, 3f)
                : Settings.ExternalPlaybackPitch;

            if (_cfgTopKeywords != null) Settings.TopKeywords = _cfgTopKeywords.Value;
            if (_cfgBottomKeywords != null) Settings.BottomKeywords = _cfgBottomKeywords.Value;
            if (_cfgBraKeywords != null) Settings.BraKeywords = _cfgBraKeywords.Value;
            if (_cfgShortsKeywords != null) Settings.ShortsKeywords = _cfgShortsKeywords.Value;
            if (_cfgGlovesKeywords != null) Settings.GlovesKeywords = _cfgGlovesKeywords.Value;
            if (_cfgPanthoseKeywords != null) Settings.PanthoseKeywords = _cfgPanthoseKeywords.Value;
            if (_cfgSocksKeywords != null) Settings.SocksKeywords = _cfgSocksKeywords.Value;
            if (_cfgShoesKeywords != null) Settings.ShoesKeywords = _cfgShoesKeywords.Value;
            if (_cfgGlassesKeywords != null) Settings.GlassesKeywords = _cfgGlassesKeywords.Value;
            if (_cfgRemoveKeywords != null) Settings.RemoveKeywords = _cfgRemoveKeywords.Value;
            if (_cfgShiftKeywords != null) Settings.ShiftKeywords = _cfgShiftKeywords.Value;
            if (_cfgPutOnKeywords != null) Settings.PutOnKeywords = _cfgPutOnKeywords.Value;
            if (_cfgRemoveAllKeywords != null) Settings.RemoveAllKeywords = _cfgRemoveAllKeywords.Value;
            if (_cfgPutOnAllKeywords != null) Settings.PutOnAllKeywords = _cfgPutOnAllKeywords.Value;
            if (_cfgCoordPattern != null) Settings.CoordPattern = _cfgCoordPattern.Value;
            if (_cfgCameraTriggerKeywords != null) Settings.CameraTriggerKeywords = _cfgCameraTriggerKeywords.Value;
            if (_cfgEnableVideoPlaybackByResponseText != null) Settings.EnableVideoPlaybackByResponseText = _cfgEnableVideoPlaybackByResponseText.Value;
            if (_cfgVideoPlaybackTriggerKeywords != null)
            {
                Settings.VideoPlaybackTriggerKeywords = string.IsNullOrWhiteSpace(_cfgVideoPlaybackTriggerKeywords.Value)
                    ? "流す"
                    : _cfgVideoPlaybackTriggerKeywords.Value.Trim();
            }
            if (_cfgSequenceSubtitleEnabled != null) Settings.SequenceSubtitleEnabled = _cfgSequenceSubtitleEnabled.Value;
            if (_cfgSequenceSubtitleHost != null) Settings.SequenceSubtitleHost = string.IsNullOrWhiteSpace(_cfgSequenceSubtitleHost.Value) ? "127.0.0.1" : _cfgSequenceSubtitleHost.Value.Trim();
            if (_cfgSequenceSubtitlePort != null) Settings.SequenceSubtitlePort = Mathf.Clamp(_cfgSequenceSubtitlePort.Value, 1, 65535);
            if (_cfgSequenceSubtitleEndpointPath != null) Settings.SequenceSubtitleEndpointPath = string.IsNullOrWhiteSpace(_cfgSequenceSubtitleEndpointPath.Value) ? "/subtitle-event" : _cfgSequenceSubtitleEndpointPath.Value.Trim();
            if (_cfgSequenceSubtitleDisplayMode != null) Settings.SequenceSubtitleDisplayMode = string.IsNullOrWhiteSpace(_cfgSequenceSubtitleDisplayMode.Value) ? "StackFemale" : _cfgSequenceSubtitleDisplayMode.Value.Trim();
            if (_cfgSequenceSubtitleSendMode != null) Settings.SequenceSubtitleSendMode = NormalizeSequenceSubtitleSendMode(_cfgSequenceSubtitleSendMode.Value);
            if (_cfgSequenceSubtitleHoldPaddingSeconds != null) Settings.SequenceSubtitleHoldPaddingSeconds = Mathf.Clamp(_cfgSequenceSubtitleHoldPaddingSeconds.Value, 0f, 5f);
            if (_cfgSequenceSubtitleProgressPrefixEnabled != null) Settings.SequenceSubtitleProgressPrefixEnabled = _cfgSequenceSubtitleProgressPrefixEnabled.Value;
            if (_cfgEnableFacePresetApply != null) Settings.EnableFacePresetApply = _cfgEnableFacePresetApply.Value;
            if (_cfgFacePresetJsonRelativePath != null)
            {
                Settings.FacePresetJsonRelativePath = string.IsNullOrWhiteSpace(_cfgFacePresetJsonRelativePath.Value)
                    ? DefaultFacePresetJsonRelativePath
                    : _cfgFacePresetJsonRelativePath.Value.Trim();
            }

            ApplyScenarioTextConfigEntriesToSettings();

            Settings.Normalize();
        }

        private void SaveSettingsToConfigJson(string reason)
        {
            if (Settings == null || string.IsNullOrWhiteSpace(PluginDir))
            {
                return;
            }

            try
            {
                SettingsStore.SaveToDefault(PluginDir, Settings);
            }
            catch (Exception ex)
            {
                LogWarn("[settings] save failed reason=" + reason + " message=" + ex.Message);
            }
        }

        private static KeyboardShortcut ParseKeyboardShortcut(string value, KeyboardShortcut fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            string[] tokens = value.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length <= 0 || !TryParseKeyCode(tokens[0], out var main))
            {
                return fallback;
            }

            if (tokens.Length == 1)
            {
                return new KeyboardShortcut(main);
            }

            var modifiers = new List<KeyCode>();
            for (int i = 1; i < tokens.Length; i++)
            {
                if (TryParseKeyCode(tokens[i], out var modifier))
                {
                    modifiers.Add(modifier);
                }
            }

            return modifiers.Count > 0
                ? new KeyboardShortcut(main, modifiers.ToArray())
                : new KeyboardShortcut(main);
        }

        private static bool TryParseKeyCode(string token, out KeyCode keyCode)
        {
            return Enum.TryParse((token ?? string.Empty).Trim(), true, out keyCode);
        }

        private KeyboardShortcut ResolveReloadKey()
        {
            if (_cfgReloadKey != null)
            {
                return _cfgReloadKey.Value;
            }

            return new KeyboardShortcut(KeyCode.R, KeyCode.LeftControl);
        }

        private KeyboardShortcut ResolveStateDumpKey()
        {
            if (_cfgStateDumpKey != null)
            {
                return _cfgStateDumpKey.Value;
            }

            return new KeyboardShortcut(KeyCode.F8, KeyCode.LeftControl, KeyCode.LeftShift);
        }

        private bool IsStateDumpKeyDown()
        {
            KeyboardShortcut dumpKey = ResolveStateDumpKey();
            return dumpKey.IsDown();
        }

        private void ReloadSettings()
        {
            string oldPipe = Settings?.PipeName ?? string.Empty;
            Settings = SettingsStore.LoadOrCreate(PluginDir, Log, LogWarn, LogError);
            LoadScenarioTextRules();
            ApplySettingsToConfigEntries(Settings);
            SaveConfigFile(reason: "reload");
            LoadPoseScoreRules();
            LoadPoseCategoryEntries();
            LogGuardSettings("reload");

            bool pipeChanged = !string.Equals(
                oldPipe,
                Settings?.PipeName ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);

            StartOrRestartPipeServer(forceRestart: pipeChanged, reason: pipeChanged ? "reload:pipe_changed" : "reload");
            Log("settings reloaded by Ctrl+R");
        }

        private void SaveConfigFile(string reason)
        {
            try
            {
                Config.Save();
            }
            catch (Exception ex)
            {
                LogWarn("[cfg] save failed reason=" + reason + " message=" + ex.Message);
            }
        }

        private void StartOrRestartPipeServer(bool forceRestart, string reason = "")
        {
            PluginSettings s = Settings;
            string reasonText = string.IsNullOrWhiteSpace(reason) ? "(none)" : reason;
            if (s == null || !s.Enabled || !s.EnablePipeServer)
            {
                LogWarn("[pipe-life] start skipped reason=" + reasonText + " enabled=" + (s != null && s.Enabled) + " pipeEnabled=" + (s != null && s.EnablePipeServer));
                StopPipeServer("start_skipped:" + reasonText);
                return;
            }

            string pipeName = CommandParser.NormalizePipeName(s.PipeName);
            LogWarn("[pipe-life] start requested reason=" + reasonText + " forceRestart=" + forceRestart + " targetPipe=" + pipeName + " currentState=" + (_pipeServer == null ? "stopped" : "running"));
            if (_pipeServer != null && !forceRestart && _pipeServer.IsForPipe(pipeName))
            {
                LogWarn("[pipe-life] start skipped reason=same_pipe_running pipe=" + pipeName + " trigger=" + reasonText);
                return;
            }

            StopPipeServer("restart:" + reasonText);

            _pipeServer = new ExternalPipeServer(
                pipeName,
                OnPipeLineReceived,
                LogAlways,
                LogWarn,
                LogError);

            _pipeServer.Start();
            LogAlways("[pipe] listening name=" + pipeName);
            LogWarn("[pipe-life] start completed reason=" + reasonText + " pipe=" + pipeName);
        }

        private void StopPipeServer(string reason = "")
        {
            if (_pipeServer == null)
            {
                if (!string.IsNullOrWhiteSpace(reason))
                {
                    LogWarn("[pipe-life] stop skipped reason=" + reason + " state=already_stopped");
                }
                return;
            }

            LogWarn("[pipe-life] stop requested reason=" + (string.IsNullOrWhiteSpace(reason) ? "(none)" : reason));
            _pipeServer.Stop();
            _pipeServer = null;
            LogWarn("[pipe-life] stop completed reason=" + (string.IsNullOrWhiteSpace(reason) ? "(none)" : reason));
        }

        private void OnPipeLineReceived(string line)
        {
            string preview = line ?? string.Empty;
            if (preview.Length > 180)
            {
                preview = preview.Substring(0, 180);
            }
            preview = preview.Replace("\r", "\\r").Replace("\n", "\\n");
            LogAlways("[pipe] recv bytes=" + Encoding.UTF8.GetByteCount(line ?? string.Empty) + " chars=" + (line ?? string.Empty).Length + " preview=" + preview);

            if (!CommandParser.TryParseIncoming(line, Settings, out var command, out var reason))
            {
                LogWarn("[pipe] parse failed reason=" + reason + " preview=" + preview);
                return;
            }

            if (command != null)
            {
                string traceId = string.IsNullOrWhiteSpace(command.traceId) ? "(none)" : command.traceId;
                string type = string.IsNullOrWhiteSpace(command.type) ? "(empty)" : command.type.Trim();
                LogAlways("[pipe] parsed type=" + type + " trace=" + traceId + " main=" + command.main + " delay=" + command.delaySeconds.ToString("F3"));
            }

            EnqueueCommand(command);
        }

        private void EnqueueCommand(ExternalVoiceFaceCommand command)
        {
            int capacity = Math.Max(1, Settings?.MaxQueuedCommands ?? 1);
            lock (_queueLock)
            {
                while (_commandQueue.Count >= capacity)
                {
                    _commandQueue.Dequeue();
                    if (Settings != null && Settings.VerboseLog)
                    {
                        LogWarn("[pipe] queue overflow, oldest command dropped");
                    }
                }

                _commandQueue.Enqueue(command);
            }
        }

        private void DrainIncomingCommands(int maxPerFrame)
        {
            for (int i = 0; i < maxPerFrame; i++)
            {
                ExternalVoiceFaceCommand command;
                lock (_queueLock)
                {
                    if (_commandQueue.Count <= 0)
                    {
                        return;
                    }

                    command = _commandQueue.Dequeue();
                }

                HandleCommand(command);
            }
        }

        private void EnqueueResponseTextCommand(ExternalVoiceFaceCommand command)
        {
            if (command == null)
            {
                return;
            }

            int capacity = Math.Max(1, Settings?.MaxQueuedCommands ?? 1);
            while (_responseTextQueue.Count >= capacity)
            {
                _responseTextQueue.Dequeue();
                LogWarn("[response_text] queue overflow, oldest command dropped");
            }

            _responseTextQueue.Enqueue(command);
            string traceId = string.IsNullOrWhiteSpace(command.traceId) ? "(none)" : command.traceId.Trim();
            _lastResponseTraceId = traceId;
            _lastResponsePhase = "queued";
            _lastResponseTextLength = string.IsNullOrWhiteSpace(command.text) ? 0 : command.text.Length;
            _lastResponseElapsedMs = -1f;
            LogAlways("[response_text] queued trace=" + traceId + " pending=" + _responseTextQueue.Count);
            DumpRuntimeState("response_text_queued:" + traceId);
        }

        private void PumpResponseTextQueue()
        {
            if (_responseTextCoroutine != null)
            {
                return;
            }

            if (_responseTextQueue.Count <= 0)
            {
                return;
            }

            ExternalVoiceFaceCommand command = _responseTextQueue.Dequeue();
            _responseTextCoroutine = StartCoroutine(RunResponseTextCommand(command));
        }

        private IEnumerator RunResponseTextCommand(ExternalVoiceFaceCommand command)
        {
            // Spread heavy text parsing away from the same frame that drained pipe commands.
            yield return null;

            string traceId = command == null || string.IsNullOrWhiteSpace(command.traceId) ? "(none)" : command.traceId.Trim();
            _lastResponseTraceId = traceId;
            _lastResponsePhase = "running";
            _lastResponseStartedAt = Time.realtimeSinceStartup;
            _lastResponseTextLength = command == null || string.IsNullOrWhiteSpace(command.text) ? 0 : command.text.Length;
            try
            {
                DumpRuntimeState("response_text_start:" + traceId);
                HandleResponseTextCommand(command);
                _lastResponsePhase = "done";
                _lastResponseElapsedMs = (Time.realtimeSinceStartup - _lastResponseStartedAt) * 1000f;
                DumpRuntimeState("response_text_done:" + traceId);
            }
            catch (Exception ex)
            {
                _lastResponsePhase = "failed";
                _lastResponseElapsedMs = (Time.realtimeSinceStartup - _lastResponseStartedAt) * 1000f;
                DumpRuntimeState("response_text_failed:" + traceId);
                LogError("[response_text] fatal trace=" + traceId + " message=" + ex.Message + "\n" + ex.StackTrace);
            }
            finally
            {
                _responseTextCoroutine = null;
            }
        }

        private void ResetResponseTextQueue(string reason)
        {
            if (_responseTextCoroutine != null)
            {
                StopCoroutine(_responseTextCoroutine);
                _responseTextCoroutine = null;
            }

            int dropped = _responseTextQueue.Count;
            _responseTextQueue.Clear();
            _lastResponsePhase = "reset:" + reason;
            if (dropped > 0)
            {
                LogWarn("[response_text] queue reset reason=" + reason + " dropped=" + dropped);
                DumpRuntimeState("response_text_reset:" + reason);
            }
        }

        private void DumpRuntimeState(string reason)
        {
            if (Settings != null && !Settings.VerboseLog)
            {
                return;
            }

            float now = Time.unscaledTime;
            bool extPlaying = _externalVoicePlayer != null && _externalVoicePlayer.IsPlaying;
            float blockRemain = Mathf.Max(0f, _blockGameVoiceUntil - now);
            bool procExists = CurrentProc != null || FindCurrentProc() != null;
            string dump =
                "[state-dump]"
                + " reason=" + reason
                + " now=" + now.ToString("F3")
                + " blockRemain=" + blockRemain.ToString("F3")
                + " blockGameVoice=" + (ShouldBlockGameVoiceEvents() ? 1 : 0)
                + " blockKiss=" + (ShouldBlockKissActions() ? 1 : 0)
                + " extPlaying=" + (extPlaying ? 1 : 0)
                + " voiceProcStopOverridden=" + (_voiceProcStopOverridden ? 1 : 0)
                + " delayedActions=" + _delayedActions.Count
                + " responseQueue=" + _responseTextQueue.Count
                + " responseRunning=" + (_responseTextCoroutine != null ? 1 : 0)
                + " lastResponseTrace=" + _lastResponseTraceId
                + " lastResponsePhase=" + _lastResponsePhase
                + " lastResponseLen=" + _lastResponseTextLength
                + " lastResponseElapsedMs=" + _lastResponseElapsedMs.ToString("F1")
                + " procExists=" + (procExists ? 1 : 0)
                + " frameStep=" + _updateLastStep;
            Log(dump);
        }

        private void HandleCommand(ExternalVoiceFaceCommand command)
        {
            var settings = Settings;
            if (command == null || settings == null)
            {
                return;
            }

            if (command.IsStop())
            {
                _externalVoicePlayer?.Stop("external stop");
                _blockGameVoiceUntil = 0f;
                RestoreVoiceProcStopIfNeeded();
                _activeSequenceSessionId = string.Empty;
                ClearDelayedActions("stop_command");
                ResetResponseTextQueue("stop_command");
                return;
            }

            if (string.Equals(command.type, "coord", StringComparison.OrdinalIgnoreCase))
            {
                HandleCoordCommand(command);
                return;
            }

            if (string.Equals(command.type, "clothes", StringComparison.OrdinalIgnoreCase))
            {
                HandleClothesCommand(command);
                return;
            }

            if (string.Equals(command.type, "response_text", StringComparison.OrdinalIgnoreCase))
            {
                EnqueueResponseTextCommand(command);
                return;
            }

            if (string.Equals(command.type, "pose", StringComparison.OrdinalIgnoreCase))
            {
                HandlePoseCommand(command);
                return;
            }

            if (string.Equals(command.type, "camera_preset", StringComparison.OrdinalIgnoreCase))
            {
                HandleCameraPresetCommand(command);
                return;
            }

            HSceneProc proc = FindCurrentProc();
            if (proc == null)
            {
                if (settings.IgnoreCommandOutsideHScene)
                {
                    if (settings.VerboseLog)
                    {
                        LogWarn("[cmd] dropped because H scene is inactive result=dropped_hscene_inactive");
                    }
                    return;
                }

                LogWarn("[cmd] HSceneProc not found result=error_hsceneproc_not_found");
                return;
            }

            int requestedMain = command.ResolveMain(settings.TargetMainIndex);
            int main = ClampMainIndex(proc, requestedMain);
            ChaControl female = ResolveFemale(proc, main);
            if (female == null)
            {
                LogWarn("[cmd] female not found main=" + main + " result=error_female_not_found");
                return;
            }

            bool keepCurrentFaceMode;
            int face = ResolveFace(command, settings, out keepCurrentFaceMode);
            string facePresetId = (command.ResolveFacePresetId() ?? string.Empty).Trim();
            string facePresetName = (command.ResolveFacePresetName() ?? string.Empty).Trim();
            bool facePresetRandom = command.ResolveFacePresetRandom(false);
            bool hasFacePresetRouting =
                !string.IsNullOrWhiteSpace(facePresetId)
                || !string.IsNullOrWhiteSpace(facePresetName)
                || facePresetRandom;
            string facePresetRouteLog =
                " facePresetName=" + (string.IsNullOrWhiteSpace(facePresetName) ? "(empty)" : facePresetName)
                + " facePresetId=" + (string.IsNullOrWhiteSpace(facePresetId) ? "(empty)" : facePresetId)
                + " facePresetRandom=" + facePresetRandom;
            string facePresetApplyResult = "not_requested";
            string facePresetSelectedName = string.Empty;
            string facePresetSelectedId = string.Empty;
            string facePresetSourcePath = string.Empty;
            int facePresetPoolCount = 0;
            int facePresetCandidateCount = 0;
            if (hasFacePresetRouting)
            {
                keepCurrentFaceMode = true;
                face = -1;
                Log(
                    "[cmd][face-preset] received"
                    + facePresetRouteLog
                    + " applyEnabled=" + settings.EnableFacePresetApply
                    + " result=accepted");

                if (!settings.EnableFacePresetApply)
                {
                    facePresetApplyResult = "disabled_by_config";
                    LogWarn(
                        "[cmd][face-preset] apply skipped"
                        + facePresetRouteLog
                        + " result=disabled_by_config");
                }
                else
                {
                    FacePresetJsonItem selectedPreset;
                    string presetReason;
                    bool appliedPreset = TryApplyFacePresetByRoute(
                        settings,
                        female,
                        facePresetName,
                        facePresetId,
                        facePresetRandom,
                        out selectedPreset,
                        out facePresetSourcePath,
                        out facePresetPoolCount,
                        out facePresetCandidateCount,
                        out presetReason);
                    if (appliedPreset)
                    {
                        facePresetApplyResult = "applied";
                        facePresetSelectedName = selectedPreset?.Name ?? string.Empty;
                        facePresetSelectedId = selectedPreset?.Id ?? string.Empty;
                        StartFacePresetProbe(
                            female,
                            facePresetName,
                            facePresetId,
                            facePresetRandom,
                            facePresetSelectedName,
                            facePresetSelectedId);
                        Log(
                            "[cmd][face-preset] apply"
                            + facePresetRouteLog
                            + " selectedName=" + (string.IsNullOrWhiteSpace(facePresetSelectedName) ? "(empty)" : facePresetSelectedName)
                            + " selectedId=" + (string.IsNullOrWhiteSpace(facePresetSelectedId) ? "(empty)" : facePresetSelectedId)
                            + " pool=" + facePresetPoolCount
                            + " candidates=" + facePresetCandidateCount
                            + " sourcePath=" + (string.IsNullOrWhiteSpace(facePresetSourcePath) ? "(empty)" : facePresetSourcePath)
                            + " result=applied");
                    }
                    else
                    {
                        facePresetApplyResult = "apply_failed";
                        LogWarn(
                            "[cmd][face-preset] apply failed"
                            + facePresetRouteLog
                            + " pool=" + facePresetPoolCount
                            + " candidates=" + facePresetCandidateCount
                            + " sourcePath=" + (string.IsNullOrWhiteSpace(facePresetSourcePath) ? "(empty)" : facePresetSourcePath)
                            + " reason=" + (string.IsNullOrWhiteSpace(presetReason) ? "unknown" : presetReason)
                            + " result=apply_failed");
                    }
                }
            }
            string facePresetExecutionLog =
                " facePresetApply=" + facePresetApplyResult
                + " facePresetSelectedName=" + (string.IsNullOrWhiteSpace(facePresetSelectedName) ? "(empty)" : facePresetSelectedName)
                + " facePresetSelectedId=" + (string.IsNullOrWhiteSpace(facePresetSelectedId) ? "(empty)" : facePresetSelectedId)
                + " facePresetPool=" + facePresetPoolCount
                + " facePresetCandidates=" + facePresetCandidateCount
                + " facePresetSourcePath=" + (string.IsNullOrWhiteSpace(facePresetSourcePath) ? "(empty)" : facePresetSourcePath);
            int voiceKind = command.ResolveVoiceKind(settings.DefaultVoiceKind);
            int action = command.ResolveAction(settings.DefaultAction);
            if (string.Equals(command.type, "speak_sequence", StringComparison.OrdinalIgnoreCase))
            {
                List<ExternalVoicePlaybackItem> playbackItems = BuildSequencePlaybackItems(command);
                if (playbackItems.Count <= 0)
                {
                    LogWarn("[cmd][seq] no playable items result=error_sequence_empty");
                    return;
                }

                BeginExternalVoiceGuard(proc, female, main);

                if (!keepCurrentFaceMode)
                {
                    TryApplyFace(proc, main, female, face, voiceKind, action);
                }

                bool interruptCurrent = command.ResolveInterrupt(settings.DefaultInterruptCurrent);
                bool deleteAfterPlay = command.ResolveDeleteAfterPlay(settings.DeleteAudioAfterPlayback);
                float defaultVolume = ResolveExternalAudioDefaultVolume(settings);
                float volume = command.ResolveVolume(defaultVolume);
                float playbackPitch = command.ResolvePitch(settings.ExternalPlaybackPitch);
                string sessionId = NormalizeSessionId(command.sessionId);

                if (interruptCurrent)
                {
                    _activeSequenceSessionId = sessionId;
                    ClearDelayedActions("sequence_interrupt:" + sessionId);
                    ResetResponseTextQueue("sequence_interrupt:" + sessionId);
                }

                bool playedSequence = _externalVoicePlayer != null && _externalVoicePlayer.PlaySequence(
                    playbackItems,
                    sessionId,
                    female,
                    interruptCurrent,
                    deleteAfterPlay,
                    volume,
                    playbackPitch);

                if (playedSequence)
                {
                    _activeSequenceSessionId = sessionId;
                    float totalDuration = 0f;
                    for (int i = 0; i < playbackItems.Count; i++)
                    {
                        totalDuration += Mathf.Max(0f, playbackItems[i].DurationSeconds);
                    }
                    Log(
                        "[cmd][seq] queued"
                        + " result=sequence_success"
                        + " session=" + sessionId
                        + " main=" + main
                        + " count=" + playbackItems.Count
                        + " totalDuration=" + totalDuration.ToString("F3")
                        + " face=" + face
                        + " keepCurrentFace=" + keepCurrentFaceMode
                        + " voiceKind=" + voiceKind
                        + " action=" + action
                        + facePresetRouteLog
                        + facePresetExecutionLog
                        + " volume=" + volume
                        + " pitch=" + playbackPitch);
                }
                else
                {
                    LogWarn(
                        "[cmd][seq] play failed"
                        + " result=sequence_failed"
                        + " session=" + sessionId
                        + " main=" + main
                        + " count=" + playbackItems.Count
                        + facePresetRouteLog
                        + facePresetExecutionLog);
                    _blockGameVoiceUntil = 0f;
                    RestoreVoiceProcStopIfNeeded();
                }

                return;
            }

            string audioPath = NormalizeAudioPath(command.ResolveAudioPath());
            if (!string.IsNullOrWhiteSpace(audioPath))
            {
                BeginExternalVoiceGuard(proc, female, main);

                if (!keepCurrentFaceMode)
                {
                    TryApplyFace(proc, main, female, face, voiceKind, action);
                }

                bool interruptCurrent = command.ResolveInterrupt(settings.DefaultInterruptCurrent);
                bool deleteAfterPlay = command.ResolveDeleteAfterPlay(settings.DeleteAudioAfterPlayback);
                float defaultVolume = ResolveExternalAudioDefaultVolume(settings);
                float volume = command.ResolveVolume(defaultVolume);
                float playbackPitch = command.ResolvePitch(settings.ExternalPlaybackPitch);

                bool playedAudio = _externalVoicePlayer != null && _externalVoicePlayer.Play(
                    audioPath,
                    female,
                    interruptCurrent,
                    deleteAfterPlay,
                    volume,
                    playbackPitch);

                if (playedAudio)
                {
                    Log(
                        "[cmd] played audio"
                        + " result=played_audio_success"
                        + " main=" + main
                        + " face=" + face
                        + " keepCurrentFace=" + keepCurrentFaceMode
                        + " voiceKind=" + voiceKind
                        + " action=" + action
                        + facePresetRouteLog
                        + facePresetExecutionLog
                        + " volume=" + volume
                        + " pitch=" + playbackPitch
                        + " audioPath=" + audioPath);
                }
                else
                {
                    LogWarn(
                        "[cmd] audio play failed"
                        + " result=played_audio_failed"
                        + " main=" + main
                        + facePresetRouteLog
                        + facePresetExecutionLog
                        + " audioPath=" + audioPath);
                    _blockGameVoiceUntil = 0f;
                    RestoreVoiceProcStopIfNeeded();
                }

                return;
            }

            if (proc.voice == null)
            {
                LogWarn("[cmd] HVoiceCtrl is not ready result=error_hvoicectrl_not_ready");
                return;
            }

            int voiceNo = ResolveVoiceNo(proc, main);
            if (voiceNo < 0)
            {
                LogWarn("[cmd] voiceNo not found main=" + main + " result=error_voice_no_not_found");
                return;
            }

            string assetBundle = command.ResolveAssetBundle(settings.DefaultAssetBundle);
            string assetName = command.ResolveAssetName(settings.DefaultAssetName);
            if (string.IsNullOrWhiteSpace(assetBundle) || string.IsNullOrWhiteSpace(assetName))
            {
                LogWarn("[cmd] assetBundle or assetName is empty result=error_asset_bundle_or_name_empty");
                return;
            }

            int eyeNeck = command.ResolveEyeNeck(settings.DefaultEyeNeck);
            float pitch = command.ResolvePitch(settings.DefaultPitch);
            float fadeTime = command.ResolveFadeTime(settings.DefaultFadeTime);

            var voiceSetting = new Illusion.Game.Utils.Voice.Setting
            {
                assetBundleName = assetBundle,
                assetName = assetName,
                no = voiceNo,
                pitch = pitch,
                fadeTime = fadeTime,
                voiceTrans = female.transform,
                settingNo = -1,
                isAsync = true,
                isPlayEndDelete = true
            };

            bool result = proc.voice.PlayVoice(
                female,
                voiceSetting,
                face,
                eyeNeck,
                voiceKind,
                action,
                main);

            if (result)
            {
                Log(
                    "[cmd] played"
                    + " result=playvoice_success"
                    + " main=" + main
                    + " voiceNo=" + voiceNo
                    + " face=" + face
                    + " keepCurrentFace=" + keepCurrentFaceMode
                    + " eyeneck=" + eyeNeck
                    + " voiceKind=" + voiceKind
                    + " action=" + action
                    + facePresetRouteLog
                    + facePresetExecutionLog
                    + " asset=" + assetName);
            }
            else
            {
                LogWarn(
                    "[cmd] PlayVoice returned false"
                    + " result=playvoice_failed"
                    + " main=" + main
                    + facePresetRouteLog
                    + facePresetExecutionLog
                    + " asset=" + assetName);
            }
        }

        private static string NormalizeSessionId(string sessionId)
        {
            string value = (sessionId ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }

            return "seq_" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        }

        private List<ExternalVoicePlaybackItem> BuildSequencePlaybackItems(ExternalVoiceFaceCommand command)
        {
            var results = new List<ExternalVoicePlaybackItem>();
            ExternalVoiceSequenceItem[] items = command?.items ?? new ExternalVoiceSequenceItem[0];
            float subtitleHoldPaddingSeconds = Settings != null ? Settings.SequenceSubtitleHoldPaddingSeconds : 0.2f;
            for (int i = 0; i < items.Length; i++)
            {
                ExternalVoiceSequenceItem item = items[i];
                if (item == null)
                {
                    continue;
                }

                string audioPath = NormalizeAudioPath(item.ResolveAudioPath());
                if (string.IsNullOrWhiteSpace(audioPath))
                {
                    LogWarn("[cmd][seq] skip item without audioPath index=" + (item.index > 0 ? item.index : i + 1));
                    continue;
                }

                results.Add(new ExternalVoicePlaybackItem
                {
                    Index = item.index > 0 ? item.index : i + 1,
                    Path = audioPath,
                    Subtitle = (item.ResolveSubtitle() ?? string.Empty).Trim(),
                    DurationSeconds = Mathf.Max(0f, item.durationSeconds),
                    HoldSeconds = Mathf.Max(0f, item.holdSeconds),
                    DeleteAfterPlay = item.deleteAfterPlay > 0
                });
            }

            int total = results.Count;
            if (total > 0)
            {
                var fullSubtitleLines = new List<string>();
                float fullHoldSeconds = 0f;
                for (int i = 0; i < total; i++)
                {
                    ExternalVoicePlaybackItem item = results[i];
                    item.Total = total;
                    item.SequencePosition = i + 1;
                    if (!string.IsNullOrWhiteSpace(item.Subtitle))
                    {
                        fullSubtitleLines.Add(item.Subtitle.Trim());
                    }

                    float itemHoldSeconds = item.HoldSeconds > 0f
                        ? item.HoldSeconds
                        : (item.DurationSeconds > 0f ? item.DurationSeconds + subtitleHoldPaddingSeconds : 0f);
                    if (itemHoldSeconds > 0f)
                    {
                        fullHoldSeconds += itemHoldSeconds;
                    }
                }

                string fullSubtitle = string.Join("\n", fullSubtitleLines.ToArray()).Trim();
                for (int i = 0; i < total; i++)
                {
                    results[i].FullSubtitle = fullSubtitle;
                    results[i].FullHoldSeconds = fullHoldSeconds;
                }
            }

            return results;
        }

        private void ClearDelayedActions(string reason)
        {
            int count = _delayedActions.Count;
            if (count <= 0)
            {
                return;
            }

            _delayedActions.Clear();
            LogWarn("[delayed] cleared reason=" + reason + " dropped=" + count);
        }

        private bool IsCurrentSession(string sessionId)
        {
            string value = (sessionId ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(_activeSequenceSessionId))
            {
                return true;
            }

            return string.Equals(value, _activeSequenceSessionId, StringComparison.Ordinal);
        }

        private Action CreateSessionGuardedAction(string sessionId, string label, Action action)
        {
            return () =>
            {
                if (!IsCurrentSession(sessionId))
                {
                    LogWarn("[session] skip stale delayed action label=" + label + " session=" + (sessionId ?? string.Empty) + " active=" + _activeSequenceSessionId);
                    return;
                }

                action();
            };
        }

        private void OnExternalVoicePlaybackStarted(ExternalVoicePlaybackStartedEvent started)
        {
            if (started == null)
            {
                return;
            }

            if (!IsCurrentSession(started.SessionId))
            {
                LogWarn("[subtitle-seq] skip stale subtitle session=" + started.SessionId + " active=" + _activeSequenceSessionId + " index=" + started.Index);
                return;
            }

            PluginSettings settings = Settings;
            if (settings == null || !settings.SequenceSubtitleEnabled)
            {
                return;
            }

            string sendMode = NormalizeSequenceSubtitleSendMode(settings.SequenceSubtitleSendMode);
            bool fullTextOnce = string.Equals(sendMode, "FullTextOnce", StringComparison.Ordinal);
            if (fullTextOnce && started.SequencePosition > 1)
            {
                Log("[subtitle-seq] skip full-text duplicate session=" + started.SessionId + " index=" + started.Index + "/" + started.Total);
                return;
            }

            string rawSubtitle = fullTextOnce ? started.FullSubtitle : started.Subtitle;
            if (string.IsNullOrWhiteSpace(rawSubtitle))
            {
                return;
            }

            string displayMode = string.IsNullOrWhiteSpace(settings.SequenceSubtitleDisplayMode)
                ? "StackFemale"
                : settings.SequenceSubtitleDisplayMode.Trim();
            string subtitleText = settings.SequenceSubtitleProgressPrefixEnabled
                ? BuildSequenceSubtitleProgressText(rawSubtitle, fullTextOnce ? 1 : started.Index, started.Total)
                : rawSubtitle;
            string text = BuildSequenceSubtitleText(subtitleText, displayMode);
            string wavName = fullTextOnce
                ? "sequence_full"
                : (string.IsNullOrWhiteSpace(started.Path) ? ("line_" + started.Index.ToString(CultureInfo.InvariantCulture)) : Path.GetFileName(started.Path));
            float holdSeconds = fullTextOnce && started.FullHoldSeconds > 0f
                ? started.FullHoldSeconds
                : (started.HoldSeconds > 0f
                ? started.HoldSeconds
                : Mathf.Max(0.1f, started.DurationSeconds + settings.SequenceSubtitleHoldPaddingSeconds));

            Log("[subtitle-seq] send start mode=" + sendMode + " session=" + started.SessionId + " index=" + started.Index + "/" + started.Total + " hold=" + holdSeconds.ToString("F3") + " text=" + TrimPreview(subtitleText, 60));
            ThreadPool.QueueUserWorkItem(_ =>
            {
                PostSequenceSubtitle(settings, text, wavName, displayMode, holdSeconds, started.SessionId, started.Index);
            });
        }

        private static string NormalizeSequenceSubtitleSendMode(string mode)
        {
            string value = (mode ?? string.Empty).Trim();
            if (string.Equals(value, "FullTextOnce", StringComparison.OrdinalIgnoreCase))
            {
                return "FullTextOnce";
            }

            return "PerLine";
        }

        private static string BuildSequenceSubtitleProgressText(string text, int index, int total)
        {
            string value = (text ?? string.Empty).Trim();
            if (value.Length <= 0 || total <= 1)
            {
                return value;
            }

            int safeIndex = index > 0 ? index : 1;
            string prefix = "[" + safeIndex.ToString(CultureInfo.InvariantCulture) + "/" + total.ToString(CultureInfo.InvariantCulture) + "] ";
            return prefix + value;
        }

        private static string BuildSequenceSubtitleText(string text, string displayMode)
        {
            string value = (text ?? string.Empty).Trim();
            if (value.Length <= 0)
            {
                return value;
            }

            if (string.Equals(displayMode, "StackFemale", StringComparison.OrdinalIgnoreCase)
                && value.IndexOf("<color=", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return "<color=#FF7ACDFF>" + value + "</color>";
            }

            return value;
        }

        private void PostSequenceSubtitle(PluginSettings settings, string text, string wavName, string displayMode, float holdSeconds, string sessionId, int index)
        {
            string host = string.IsNullOrWhiteSpace(settings.SequenceSubtitleHost) ? "127.0.0.1" : settings.SequenceSubtitleHost.Trim();
            int port = settings.SequenceSubtitlePort;
            if (port <= 0 || port > 65535)
            {
                port = 18766;
            }

            string endpoint = string.IsNullOrWhiteSpace(settings.SequenceSubtitleEndpointPath)
                ? "/subtitle-event"
                : settings.SequenceSubtitleEndpointPath.Trim();
            if (!endpoint.StartsWith("/", StringComparison.Ordinal))
            {
                endpoint = "/" + endpoint;
            }

            string url = "http://" + host + ":" + port + endpoint;
            string holdText = Mathf.Max(0.1f, holdSeconds).ToString("0.###", CultureInfo.InvariantCulture);
            string payload =
                "{\"text\":\"" + EscapeJsonValue(text)
                + "\",\"source\":\"voiceface_sequence\""
                + ",\"wav_name\":\"" + EscapeJsonValue(wavName)
                + "\",\"display_mode\":\"" + EscapeJsonValue(displayMode)
                + "\",\"speaker_gender\":\"female\""
                + ",\"session_id\":\"" + EscapeJsonValue(sessionId)
                + "\",\"index\":" + index.ToString(CultureInfo.InvariantCulture)
                + ",\"hold_seconds\":" + holdText
                + "}";

            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.ContentType = "application/json; charset=utf-8";
                request.Timeout = 1500;
                request.ReadWriteTimeout = 1500;
                if (!string.IsNullOrWhiteSpace(settings.SequenceSubtitleToken))
                {
                    request.Headers["X-Auth-Token"] = settings.SequenceSubtitleToken.Trim();
                }

                byte[] body = Encoding.UTF8.GetBytes(payload);
                request.ContentLength = body.Length;
                using (Stream stream = request.GetRequestStream())
                {
                    stream.Write(body, 0, body.Length);
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    string responseText = reader.ReadToEnd();
                    Log("[subtitle-seq] sent status=" + (int)response.StatusCode + " session=" + sessionId + " index=" + index);
                    if (!string.IsNullOrWhiteSpace(responseText) && Settings != null && Settings.VerboseLog)
                    {
                        Log("[subtitle-seq] response: " + TrimPreview(responseText, 120));
                    }
                }
            }
            catch (WebException webEx)
            {
                string detail = webEx.Message;
                try
                {
                    if (webEx.Response != null)
                    {
                        using (var stream = webEx.Response.GetResponseStream())
                        using (var reader = new StreamReader(stream ?? Stream.Null, Encoding.UTF8))
                        {
                            string body = reader.ReadToEnd();
                            if (!string.IsNullOrWhiteSpace(body))
                            {
                                detail += " body=" + TrimPreview(body, 120);
                            }
                        }
                    }
                }
                catch { }
                LogWarn("[subtitle-seq] request failed: " + detail + " url=" + url);
            }
            catch (Exception ex)
            {
                LogWarn("[subtitle-seq] request error: " + ex.Message + " url=" + url);
            }
        }

        internal bool ShouldBlockGameVoiceEvents()
        {
            var s = Settings;
            if (s == null || !s.Enabled || !s.BlockGameVoiceWhileExternalPlaying)
            {
                return false;
            }

            if (_externalVoicePlayer != null && _externalVoicePlayer.IsPlaying)
            {
                return true;
            }

            return Time.unscaledTime < _blockGameVoiceUntil;
        }

        internal bool ShouldBlockKissActions()
        {
            var s = Settings;
            if (s == null || !s.Enabled)
            {
                return false;
            }

            if (_externalVoicePlayer != null && _externalVoicePlayer.IsPlaying)
            {
                return true;
            }

            return Time.unscaledTime < _blockGameVoiceUntil;
        }

        internal void OnGameVoiceEventBlocked(string point)
        {
            var s = Settings;
            if (s == null || !s.VerboseLog || string.IsNullOrWhiteSpace(point))
            {
                return;
            }

            float now = Time.unscaledTime;
            if (_blockLogCooldownByKey.TryGetValue(point, out var nextAllowed) && now < nextAllowed)
            {
                return;
            }

            _blockLogCooldownByKey[point] = now + 1f;
            Log("[block] " + point);
            DumpRuntimeState("game_voice_blocked:" + point);
        }

        internal void OnKissActionBlocked(string point)
        {
            if (string.IsNullOrWhiteSpace(point))
            {
                return;
            }

            float now = Time.unscaledTime;
            string key = "kiss:" + point;
            if (_blockLogCooldownByKey.TryGetValue(key, out var nextAllowed) && now < nextAllowed)
            {
                return;
            }

            _blockLogCooldownByKey[key] = now + 1f;
            bool extPlaying = _externalVoicePlayer != null && _externalVoicePlayer.IsPlaying;
            float remain = Mathf.Max(0f, _blockGameVoiceUntil - now);
            Log(
                "[kiss-block] "
                + point
                + " extPlaying=" + extPlaying
                + " blockRemain=" + remain.ToString("0.###"));
            DumpRuntimeState("kiss_blocked:" + point);
        }

        private void BeginExternalVoiceGuard(HSceneProc proc, ChaControl female, int main)
        {
            var s = Settings;
            if (s != null)
            {
                float until = Time.unscaledTime + s.ExternalPlayPreBlockSeconds;
                if (until > _blockGameVoiceUntil)
                {
                    _blockGameVoiceUntil = until;
                }
            }

            TryStopKissAction(proc, main, "external-play-start");

            if (s == null || !s.StopGameVoiceBeforeExternalPlay)
            {
                return;
            }

            TryStopGameVoice(proc, female, main);
        }

        private void TryStopKissAction(HSceneProc proc, int main, string reason)
        {
            try
            {
                HandCtrl hand = ResolveHand(proc, main);
                if (hand == null)
                {
                    return;
                }

                if (!hand.IsKissAction())
                {
                    return;
                }

                bool result = hand.ForceFinish();
                Log(
                    "[kiss-block] force-finish active kiss"
                    + " main=" + main
                    + " result=" + result
                    + " reason=" + reason);
            }
            catch (Exception ex)
            {
                LogWarn("[kiss-block] force-finish failed main=" + main + " reason=" + reason + " ex=" + ex.Message);
            }
        }

        private static HandCtrl ResolveHand(HSceneProc proc, int main)
        {
            if (proc == null)
            {
                return null;
            }

            if (main <= 0)
            {
                return proc.hand;
            }

            return proc.hand1 ?? proc.hand;
        }

        private void TryStopGameVoice(HSceneProc proc, ChaControl female, int main)
        {
            try
            {
                int targets = 0;
                int stoppedTargets = 0;
                int residualBefore = 0;
                int residualAfter = 0;

                if (proc != null && proc.flags != null && proc.flags.transVoiceMouth != null)
                {
                    for (int i = 0; i < proc.flags.transVoiceMouth.Length; i++)
                    {
                        Transform voiceTrans = proc.flags.transVoiceMouth[i];
                        if (voiceTrans == null)
                        {
                            continue;
                        }

                        targets++;
                        bool wasPlaying = Manager.Voice.IsPlay(voiceTrans);
                        if (wasPlaying)
                        {
                            residualBefore++;
                        }

                        int voiceNo = -1;
                        if (proc.flags.lstHeroine != null && i >= 0 && i < proc.flags.lstHeroine.Count && proc.flags.lstHeroine[i] != null)
                        {
                            voiceNo = proc.flags.lstHeroine[i].voiceNo;
                        }

                        if (voiceNo >= 0)
                        {
                            Manager.Voice.Stop(voiceNo, voiceTrans);
                        }

                        Manager.Voice.Stop(voiceTrans);

                        if (Manager.Voice.IsPlay(voiceTrans))
                        {
                            if (voiceNo >= 0)
                            {
                                Manager.Voice.Stop(voiceNo, voiceTrans);
                            }
                            Manager.Voice.Stop(voiceTrans);
                        }

                        if (!Manager.Voice.IsPlay(voiceTrans))
                        {
                            stoppedTargets++;
                        }
                        else
                        {
                            residualAfter++;
                        }
                    }
                }

                if (female != null)
                {
                    Manager.Voice.Stop(female.transform);
                    ClearLipSyncSource(female);
                }

                if (proc != null && proc.voice != null)
                {
                    if (!_voiceProcStopOverridden)
                    {
                        _voiceProcStopOriginal = proc.voice.isVoicePrcoStop;
                        _voiceProcStopOverridden = true;
                    }

                    proc.voice.isVoicePrcoStop = true;
                }

                if (residualAfter > 0)
                {
                    Manager.Voice.StopAll(false);
                }

                bool anyPlayingAfter = Manager.Voice.IsPlay();
                Log(
                    "[guard] force-stop game voice before external play"
                    + " main=" + main
                    + " targets=" + targets
                    + " residualBefore=" + residualBefore
                    + " stoppedTargets=" + stoppedTargets
                    + " residualAfter=" + residualAfter
                    + " anyPlayingAfter=" + anyPlayingAfter);
            }
            catch (Exception ex)
            {
                LogWarn("[guard] stop game voice failed: " + ex.Message);
            }
        }

        private static void ClearLipSyncSource(ChaControl female)
        {
            if (female == null)
            {
                return;
            }

            try
            {
                female.SetLipSync(null);
            }
            catch
            {
            }
        }

        private static float ResolveExternalAudioDefaultVolume(PluginSettings settings)
        {
            if (settings == null)
            {
                return 1f;
            }

            if (settings.FemalePlaybackVolume >= 0f)
            {
                return Mathf.Clamp01(settings.FemalePlaybackVolume);
            }

            return Mathf.Clamp01(settings.PlaybackVolume);
        }

        private void LogGuardSettings(string reason)
        {
            var s = Settings;
            if (s == null)
            {
                return;
            }

            Log(
                "[guard] settings"
                + " reason=" + reason
                + " stopBefore=" + s.StopGameVoiceBeforeExternalPlay
                + " blockWhilePlay=" + s.BlockGameVoiceWhileExternalPlaying
                + " preBlockSec=" + s.ExternalPlayPreBlockSeconds
                + " vol=" + s.PlaybackVolume
                + " femaleVol=" + s.FemalePlaybackVolume
                + " extPitch=" + s.ExternalPlaybackPitch);
        }

        private void RestoreVoiceProcStopIfNeeded()
        {
            if (!_voiceProcStopOverridden)
            {
                return;
            }

            HSceneProc proc = CurrentProc ?? FindCurrentProc();
            if (proc == null || proc.voice == null)
            {
                float now = Time.unscaledTime;
                if (now >= _nextVoiceRestorePendingLogTime)
                {
                    _nextVoiceRestorePendingLogTime = now + 1f;
                    LogWarn(
                        "[guard] restore pending: keep override because "
                        + (proc == null ? "proc is null" : "proc.voice is null"));
                }

                return;
            }

            try
            {
                bool before = proc.voice.isVoicePrcoStop;
                proc.voice.isVoicePrcoStop = _voiceProcStopOriginal;
                bool after = proc.voice.isVoicePrcoStop;
                Log(
                    "[guard] restore applied"
                    + " before=" + before
                    + " after=" + after
                    + " original=" + _voiceProcStopOriginal);
                _voiceProcStopOverridden = false;
                _voiceProcStopOriginal = false;
                _nextVoiceRestorePendingLogTime = 0f;
            }
            catch (Exception ex)
            {
                float now = Time.unscaledTime;
                if (now >= _nextVoiceRestorePendingLogTime)
                {
                    _nextVoiceRestorePendingLogTime = now + 1f;
                    LogWarn("[guard] restore failed: " + ex.Message);
                }
            }
        }

        // ----------------------------------------------------------------
        // 遅延アクション処理
        // ----------------------------------------------------------------

        private void ProcessDelayedActions()
        {
            float now = Time.unscaledTime;
            for (int i = _delayedActions.Count - 1; i >= 0; i--)
            {
                if (now >= _delayedActions[i].Item1)
                {
                    try { _delayedActions[i].Item2(); }
                    catch (Exception ex) { LogWarn("[delayed] 実行失敗: " + ex.Message); }
                    _delayedActions.RemoveAt(i);
                }
            }
        }

        // ----------------------------------------------------------------
        // response_text コマンド: 生テキストをパースして着替え/着衣を遅延実行
        // ----------------------------------------------------------------

        private void HandleResponseTextCommand(ExternalVoiceFaceCommand command)
        {
            string text = (command.text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(text)) return;
            string traceId = string.IsNullOrWhiteSpace(command.traceId) ? "(none)" : command.traceId.Trim();
            string sessionId = (command.sessionId ?? string.Empty).Trim();
            if (!IsCurrentSession(sessionId))
            {
                LogWarn("[response_text] skip stale session trace=" + traceId + " session=" + sessionId + " active=" + _activeSequenceSessionId);
                return;
            }

            Log($"[response_text] received trace={traceId} len={text.Length} delay={Mathf.Max(0f, command.delaySeconds):F3} preview={text.Substring(0, Math.Min(60, text.Length))}");
            float _rtStart = Time.realtimeSinceStartup;

            float baseScheduleTime = Time.unscaledTime;
            float totalDelaySeconds = Mathf.Max(0f, command.delaySeconds);
            int main = command.ResolveMain(Settings?.TargetMainIndex ?? 0);

            string[] coordTriggers = SplitKeywords(_cfgCoordPattern?.Value ?? "着替え");
            List<TimedCoordItem> coordItems = FindCoordMatchesFromText(text, coordTriggers, main);
            if (coordItems != null && coordItems.Count > 0)
            {
                foreach (TimedCoordItem timed in coordItems)
                {
                    if (timed == null || string.IsNullOrWhiteSpace(timed.CoordName))
                    {
                        continue;
                    }

                    float coordDelaySeconds = ComputeActionDelaySecondsByTextPosition(text, totalDelaySeconds, timed.MatchIndex);
                    float coordExecuteAt = baseScheduleTime + coordDelaySeconds;
                    string cn = timed.CoordName;
                    string trigger = timed.TriggerKeyword ?? string.Empty;
                    int matchIndex = timed.MatchIndex;
                    int m = main;
                    _delayedActions.Add(Tuple.Create(coordExecuteAt, CreateSessionGuardedAction(sessionId, "coord", () =>
                    {
                        HandleCoordCommand(new ExternalVoiceFaceCommand { type = "coord", coordName = cn, main = m });
                    })));
                    Log($"[response_text] coord matched: '{cn}', trigger='{trigger}', pos={matchIndex}, scheduled delay={coordDelaySeconds:F2}s");
                }
            }
            else if (ContainsAny(text, coordTriggers))
            {
                Log("[response_text] coord trigger found but no slot name matched before trigger in same line");
            }
            else
            {
                Log("[response_text] no coord keyword matched");
            }

            if (TryPickPoseFromText(text, out var poseName, out var poseMode, out var poseCategory, out var poseMatchIndex))
            {
                float poseDelaySeconds = ComputeActionDelaySecondsByTextPosition(text, totalDelaySeconds, poseMatchIndex);
                float poseExecuteAt = baseScheduleTime + poseDelaySeconds;
                Log($"[response_text] pose category matched: '{poseCategory}' -> '{poseName}' (mode={poseMode}) pos={poseMatchIndex}, scheduled delay={poseDelaySeconds:F2}s");
                string pn = poseName;
                int pm = poseMode;
                int m = main;
                _delayedActions.Add(Tuple.Create(poseExecuteAt, CreateSessionGuardedAction(sessionId, "pose", () =>
                {
                    HandlePoseCommand(new ExternalVoiceFaceCommand { type = "pose", poseName = pn, poseMode = pm, main = m });
                })));
            }
            else
            {
                Log("[response_text] no pose keyword matched");
            }

            List<CameraPresetTriggerHit> cameraHits = FindCameraPresetTriggerHitsFromText(text);
            if (cameraHits != null && cameraHits.Count > 0)
            {
                foreach (CameraPresetTriggerHit hit in cameraHits)
                {
                    if (hit == null || string.IsNullOrWhiteSpace(hit.PresetName))
                    {
                        continue;
                    }

                    float cameraDelaySeconds = ComputeActionDelaySecondsByTextPosition(text, totalDelaySeconds, hit.MatchIndex);
                    float cameraExecuteAt = baseScheduleTime + cameraDelaySeconds;
                    string presetNameCopy = hit.PresetName;
                    int lineIndexCopy = hit.LineIndex;
                    string triggerCopy = hit.TriggerKeyword ?? string.Empty;
                    _delayedActions.Add(Tuple.Create(cameraExecuteAt, CreateSessionGuardedAction(sessionId, "camera_preset", () =>
                    {
                        HandleCameraPresetCommand(new ExternalVoiceFaceCommand
                        {
                            type = "camera_preset",
                            cameraPresetName = presetNameCopy
                        });
                    })));

                    Log($"[response_text] camera matched preset='{presetNameCopy}' trigger='{triggerCopy}' line={lineIndexCopy + 1} pos={hit.MatchIndex}, scheduled delay={cameraDelaySeconds:F2}s");
                }
            }
            else
            {
                Log("[response_text] no camera keyword matched");
            }

            List<TimedClothesItem> clothesItems = ParseTimedClothesFromText(text);
            if (clothesItems != null && clothesItems.Count > 0)
            {
                foreach (TimedClothesItem timed in clothesItems)
                {
                    if (timed == null || timed.Item == null)
                    {
                        continue;
                    }

                    float clothesDelaySeconds = ComputeActionDelaySecondsByTextPosition(text, totalDelaySeconds, timed.MatchIndex);
                    float clothesExecuteAt = baseScheduleTime + clothesDelaySeconds;
                    ClothesItem itemCopy = new ClothesItem { kind = timed.Item.kind, state = timed.Item.state };
                    int m = main;
                    _delayedActions.Add(Tuple.Create(clothesExecuteAt, CreateSessionGuardedAction(sessionId, "clothes", () =>
                    {
                        HandleClothesCommand(new ExternalVoiceFaceCommand
                        {
                            type = "clothes",
                            clothesItems = new[] { itemCopy },
                            main = m
                        });
                    })));

                    Log($"[response_text] clothes matched kind={itemCopy.kind} state={itemCopy.state} part='{timed.PartKeyword}' action='{timed.ActionKeyword}' pos={timed.MatchIndex}, scheduled delay={clothesDelaySeconds:F2}s");
                }
            }
            else
            {
                Log("[response_text] no clothes keywords matched");
            }

            if (TryParseTimedGlassesStateFromText(text, out int glassesState, out int glassesMatchIndex, out string glassesPart, out string glassesAction))
            {
                bool showGlasses = glassesState > 0;
                float glassesDelaySeconds = ComputeActionDelaySecondsByTextPosition(text, totalDelaySeconds, glassesMatchIndex);
                float glassesExecuteAt = baseScheduleTime + glassesDelaySeconds;
                Log($"[response_text] glasses matched action={(showGlasses ? "put_on" : "remove")} part='{glassesPart}' keyword='{glassesAction}' pos={glassesMatchIndex}, scheduled delay={glassesDelaySeconds:F2}s");
                int m = main;
                bool s = showGlasses;
                _delayedActions.Add(Tuple.Create(glassesExecuteAt, CreateSessionGuardedAction(sessionId, "glasses", () =>
                {
                    HandleGlassesToggle(m, s);
                })));
            }
            else
            {
                Log("[response_text] no glasses keywords matched");
            }

            if (TrySelectVideoFileNameFromText(text, out string videoFileName, out int httpPort, out string videoReason))
            {
                string[] videoTriggerKeywords = SplitKeywords((Settings?.VideoPlaybackTriggerKeywords ?? "流す").Replace('、', ','));
                int videoMatchIndex = FindFirstKeywordIndex(text, videoTriggerKeywords, StringComparison.Ordinal);
                float videoDelaySeconds = ComputeActionDelaySecondsByTextPosition(text, totalDelaySeconds, videoMatchIndex);
                float videoExecuteAt = baseScheduleTime + videoDelaySeconds;
                string selectedVideo = videoFileName;
                int selectedPort = httpPort;
                _delayedActions.Add(Tuple.Create(videoExecuteAt, CreateSessionGuardedAction(sessionId, "video", () =>
                {
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        PostVideoPlayByFileName(selectedVideo, selectedPort);
                    });
                })));
                Log($"[response_text] video matched: filename='{selectedVideo}' port={selectedPort} pos={videoMatchIndex}, scheduled delay={videoDelaySeconds:F2}s");
            }
            else
            {
                Log("[response_text] no video keyword matched (" + videoReason + ")");
            }
            Log($"[response_text] done trace={traceId} elapsed={(Time.realtimeSinceStartup - _rtStart) * 1000f:F1}ms len={text.Length}");
        }

        private static PoseKeywordScoreToken CreatePoseScoreToken(string keyword, int score)
        {
            return new PoseKeywordScoreToken
            {
                Keyword = keyword,
                Score = score
            };
        }

        private static PoseKeywordScoreRule CreatePoseScoreRule(
            string ruleId,
            string category,
            int priority,
            string[] poseNames,
            params PoseKeywordScoreToken[] tokens)
        {
            return new PoseKeywordScoreRule
            {
                RuleId = ruleId,
                Category = category,
                Priority = priority,
                PoseNames = poseNames ?? new string[0],
                Tokens = tokens ?? new PoseKeywordScoreToken[0]
            };
        }

        private static PoseCategoryInferRule CreatePoseInferRule(
            string ruleId,
            string targetCategory,
            int priority,
            string[] requiredAll,
            string[] requiredAny,
            string[] excludeAny)
        {
            return new PoseCategoryInferRule
            {
                RuleId = ruleId,
                TargetCategory = targetCategory,
                Priority = priority,
                RequiredAll = requiredAll ?? new string[0],
                RequiredAny = requiredAny ?? new string[0],
                ExcludeAny = excludeAny ?? new string[0]
            };
        }

        private void LoadPoseScoreRules()
        {
            string path = Path.Combine(PluginDir ?? string.Empty, PoseScoreRulesFileName);
            _poseScoreRulesFilePath = path;
            try
            {
                PoseScoreRulesFile file;
                bool wroteBack = false;
                if (!File.Exists(path))
                {
                    file = CreateDefaultPoseScoreRulesFile();
                    SavePoseScoreRulesFile(path, file);
                    wroteBack = true;
                    Log($"[pose-score] default file created: {path}");
                }
                else
                {
                    file = DeserializePoseScoreRulesFile(path);
                    if (TryMigratePoseScoreRulesFile(file))
                    {
                        SavePoseScoreRulesFile(path, file);
                        wroteBack = true;
                        Log($"[pose-score] migrated file updated: {path}");
                    }
                }

                ApplyPoseScoreRulesFile(file, source: path);
                EnsurePoseControlConfigEntries();
                SyncPoseControlConfigEntriesFromRuntime();
                if (wroteBack)
                {
                    Log($"[pose-score] active file saved: {path}");
                }
            }
            catch (Exception ex)
            {
                LogWarn("[pose-score] load failed, fallback to built-in defaults. message=" + ex.Message);
                ApplyPoseScoreRulesFile(CreateDefaultPoseScoreRulesFile(), source: "built-in-default");
                EnsurePoseControlConfigEntries();
                SyncPoseControlConfigEntriesFromRuntime();
            }
        }

        private bool TryMigratePoseScoreRulesFile(PoseScoreRulesFile file)
        {
            if (file == null)
            {
                return false;
            }

            bool changed = false;
            if (file.Version < PoseScoreRulesCurrentVersion)
            {
                file.Version = PoseScoreRulesCurrentVersion;
                changed = true;
            }

            if (!file.Enabled.HasValue)
            {
                file.Enabled = true;
                changed = true;
            }
            if (!file.SimpleModeEnabled.HasValue)
            {
                file.SimpleModeEnabled = false;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(file.SimpleModeTriggerKeywords))
            {
                file.SimpleModeTriggerKeywords = DefaultPoseSimpleModeTriggerKeywords;
                changed = true;
            }
            else
            {
                string normalizedSimpleTriggers = NormalizePoseSimpleModeTriggerKeywords(file.SimpleModeTriggerKeywords);
                if (!string.Equals(file.SimpleModeTriggerKeywords, normalizedSimpleTriggers, StringComparison.Ordinal))
                {
                    file.SimpleModeTriggerKeywords = normalizedSimpleTriggers;
                    changed = true;
                }
            }
            if (!file.RulesEnabled.HasValue)
            {
                file.RulesEnabled = true;
                changed = true;
            }
            if (!file.InferRulesEnabled.HasValue)
            {
                file.InferRulesEnabled = true;
                changed = true;
            }

            if (file.Rules == null)
            {
                file.Rules = new List<PoseKeywordScoreRule>();
                changed = true;
            }

            int customRuleIndex = 0;
            var ruleIdSet = new HashSet<string>(StringComparer.Ordinal);
            foreach (PoseKeywordScoreRule rule in file.Rules)
            {
                if (rule == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(rule.RuleId))
                {
                    rule.RuleId = "rule_custom_" + customRuleIndex++;
                    changed = true;
                }
                if (!rule.Enabled.HasValue)
                {
                    rule.Enabled = true;
                    changed = true;
                }

                ruleIdSet.Add(rule.RuleId);
            }

            foreach (PoseKeywordScoreRule builtIn in PoseKeywordScoreRules.Where(r => r != null))
            {
                if (string.IsNullOrWhiteSpace(builtIn.RuleId) || ruleIdSet.Contains(builtIn.RuleId))
                {
                    continue;
                }

                file.Rules.Add(ClonePoseScoreRule(builtIn));
                ruleIdSet.Add(builtIn.RuleId);
                changed = true;
            }

            if (file.InferRules == null)
            {
                file.InferRules = new List<PoseCategoryInferRule>();
                changed = true;
            }

            int customInferIndex = 0;
            var inferRuleIdSet = new HashSet<string>(StringComparer.Ordinal);
            foreach (PoseCategoryInferRule rule in file.InferRules)
            {
                if (rule == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(rule.RuleId))
                {
                    rule.RuleId = "infer_custom_" + customInferIndex++;
                    changed = true;
                }
                if (!rule.Enabled.HasValue)
                {
                    rule.Enabled = true;
                    changed = true;
                }
                inferRuleIdSet.Add(rule.RuleId);
            }

            foreach (PoseCategoryInferRule builtIn in PoseCategoryInferenceRules.Where(r => r != null))
            {
                if (string.IsNullOrWhiteSpace(builtIn.RuleId) || inferRuleIdSet.Contains(builtIn.RuleId))
                {
                    continue;
                }

                file.InferRules.Add(ClonePoseInferRule(builtIn));
                inferRuleIdSet.Add(builtIn.RuleId);
                changed = true;
            }

            if (file.CategoryEnabled == null)
            {
                file.CategoryEnabled = new Dictionary<string, bool>(StringComparer.Ordinal);
                changed = true;
            }

            var allCategories = new HashSet<string>(StringComparer.Ordinal);
            if (file.Rules != null)
            {
                foreach (PoseKeywordScoreRule rule in file.Rules)
                {
                    if (rule != null && !string.IsNullOrWhiteSpace(rule.Category))
                    {
                        allCategories.Add(rule.Category);
                    }
                }
            }
            if (file.InferRules != null)
            {
                foreach (PoseCategoryInferRule rule in file.InferRules)
                {
                    if (rule != null && !string.IsNullOrWhiteSpace(rule.TargetCategory))
                    {
                        allCategories.Add(rule.TargetCategory);
                    }
                }
            }

            foreach (string category in allCategories)
            {
                if (!file.CategoryEnabled.ContainsKey(category))
                {
                    file.CategoryEnabled[category] = true;
                    changed = true;
                }
            }

            return changed;
        }

        private void ApplyPoseScoreRulesFile(PoseScoreRulesFile file, string source)
        {
            if (file == null)
            {
                file = CreateDefaultPoseScoreRulesFile();
                source = "built-in-default(null)";
            }

            _poseChangeEnabled = file.Enabled != false;
            _poseSimpleModeEnabled = file.SimpleModeEnabled == true;
            _poseSimpleModeTriggerKeywords = NormalizePoseSimpleModeTriggerKeywords(file.SimpleModeTriggerKeywords);
            _poseRulesEnabled = file.RulesEnabled != false;
            _poseInferRulesEnabled = file.InferRulesEnabled != false;
            _poseScoreBase = file.ScoreBase > 0 ? file.ScoreBase : DefaultPoseScoreBase;
            _poseAdoptThreshold = file.AdoptThreshold > 0 ? file.AdoptThreshold : DefaultPoseAdoptThreshold;
            _poseForceThreshold = file.ForceThreshold > 0 ? file.ForceThreshold : DefaultPoseForceThreshold;
            if (_poseForceThreshold < _poseAdoptThreshold)
            {
                _poseForceThreshold = _poseAdoptThreshold;
            }

            _categoryAliases = new Dictionary<string, string[]>(StringComparer.Ordinal);
            if (file.CategoryAliases != null)
            {
                foreach (var kv in file.CategoryAliases)
                {
                    if (!string.IsNullOrWhiteSpace(kv.Key) && kv.Value != null)
                        _categoryAliases[kv.Key] = kv.Value;
                }
            }

            var normalized = new List<PoseKeywordScoreRule>();
            if (file.Rules != null)
            {
                foreach (PoseKeywordScoreRule rule in file.Rules)
                {
                    if (rule == null || string.IsNullOrWhiteSpace(rule.Category))
                    {
                        continue;
                    }

                    string[] poseNames = (rule.PoseNames ?? new string[0])
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    if (poseNames.Length <= 0)
                    {
                        continue;
                    }

                    PoseKeywordScoreToken[] tokens = (rule.Tokens ?? new PoseKeywordScoreToken[0])
                        .Where(t => t != null && !string.IsNullOrWhiteSpace(t.Keyword))
                        .Select(t => new PoseKeywordScoreToken
                        {
                            Keyword = t.Keyword,
                            Score = t.Score > 0 ? t.Score : 1
                        })
                        .ToArray();
                    if (tokens.Length <= 0)
                    {
                        continue;
                    }

                    normalized.Add(new PoseKeywordScoreRule
                    {
                        RuleId = string.IsNullOrWhiteSpace(rule.RuleId) ? ("rule_" + normalized.Count) : rule.RuleId,
                        Category = rule.Category,
                        Priority = rule.Priority,
                        PoseNames = poseNames,
                        Tokens = tokens,
                        Enabled = rule.Enabled != false
                    });
                }
            }

            var normalizedInferRules = new List<PoseCategoryInferRule>();
            if (file.InferRules != null)
            {
                foreach (PoseCategoryInferRule rule in file.InferRules)
                {
                    if (rule == null || string.IsNullOrWhiteSpace(rule.TargetCategory))
                    {
                        continue;
                    }

                    string[] requiredAll = (rule.RequiredAll ?? new string[0])
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.Ordinal)
                        .ToArray();
                    string[] requiredAny = (rule.RequiredAny ?? new string[0])
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.Ordinal)
                        .ToArray();
                    string[] excludeAny = (rule.ExcludeAny ?? new string[0])
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.Ordinal)
                        .ToArray();

                    if (requiredAll.Length <= 0 && requiredAny.Length <= 0)
                    {
                        continue;
                    }

                    normalizedInferRules.Add(new PoseCategoryInferRule
                    {
                        RuleId = string.IsNullOrWhiteSpace(rule.RuleId) ? ("infer_" + normalizedInferRules.Count) : rule.RuleId,
                        TargetCategory = rule.TargetCategory,
                        Priority = rule.Priority,
                        RequiredAll = requiredAll,
                        RequiredAny = requiredAny,
                        ExcludeAny = excludeAny,
                        Enabled = rule.Enabled != false
                    });
                }
            }

            _poseKeywordScoreRules = normalized;
            _poseCategoryInferRules = normalizedInferRules;
            RebuildPoseCategoryEnabledMap(file.CategoryEnabled);
            Log($"[pose-score] loaded rules={_poseKeywordScoreRules.Count} inferRules={_poseCategoryInferRules.Count} simpleMode={_poseSimpleModeEnabled} simpleTriggers='{_poseSimpleModeTriggerKeywords}' rulesEnabled={_poseRulesEnabled} inferEnabled={_poseInferRulesEnabled} scoreBase={_poseScoreBase} adopt={_poseAdoptThreshold} force={_poseForceThreshold} source={source}");
        }

        private void RebuildPoseCategoryEnabledMap(Dictionary<string, bool> fromFile)
        {
            _poseCategoryEnabled.Clear();

            var categorySet = new HashSet<string>(StringComparer.Ordinal);
            foreach (PoseKeywordScoreRule rule in _poseKeywordScoreRules)
            {
                if (rule == null || string.IsNullOrWhiteSpace(rule.Category))
                {
                    continue;
                }

                categorySet.Add(rule.Category);
            }

            foreach (PoseCategoryInferRule rule in _poseCategoryInferRules)
            {
                if (rule == null || string.IsNullOrWhiteSpace(rule.TargetCategory))
                {
                    continue;
                }

                categorySet.Add(rule.TargetCategory);
            }

            foreach (string category in categorySet)
            {
                bool enabled = true;
                if (fromFile != null && fromFile.TryGetValue(category, out bool v))
                {
                    enabled = v;
                }

                _poseCategoryEnabled[category] = enabled;
            }
        }

        private PoseScoreRulesFile CreateDefaultPoseScoreRulesFile()
        {
            var rules = PoseKeywordScoreRules
                .Where(r => r != null)
                .Select(ClonePoseScoreRule)
                .ToList();
            var inferRules = PoseCategoryInferenceRules
                .Where(r => r != null)
                .Select(ClonePoseInferRule)
                .ToList();
            var categoryEnabled = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (var c in rules.Select(r => r.Category).Concat(inferRules.Select(r => r.TargetCategory)).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal))
            {
                categoryEnabled[c] = true;
            }

            return new PoseScoreRulesFile
            {
                Version = PoseScoreRulesCurrentVersion,
                Enabled = true,
                SimpleModeEnabled = false,
                SimpleModeTriggerKeywords = DefaultPoseSimpleModeTriggerKeywords,
                RulesEnabled = true,
                InferRulesEnabled = true,
                ScoreBase = DefaultPoseScoreBase,
                AdoptThreshold = DefaultPoseAdoptThreshold,
                ForceThreshold = DefaultPoseForceThreshold,
                Rules = rules,
                InferRules = inferRules,
                CategoryEnabled = categoryEnabled
            };
        }

        private static PoseKeywordScoreRule ClonePoseScoreRule(PoseKeywordScoreRule rule)
        {
            if (rule == null)
            {
                return null;
            }

            return new PoseKeywordScoreRule
            {
                RuleId = rule.RuleId,
                Category = rule.Category,
                Priority = rule.Priority,
                Enabled = rule.Enabled != false,
                PoseNames = (rule.PoseNames ?? new string[0]).ToArray(),
                Tokens = (rule.Tokens ?? new PoseKeywordScoreToken[0])
                    .Where(t => t != null)
                    .Select(t => new PoseKeywordScoreToken
                    {
                        Keyword = t.Keyword,
                        Score = t.Score
                    })
                    .ToArray()
            };
        }

        private static PoseCategoryInferRule ClonePoseInferRule(PoseCategoryInferRule rule)
        {
            if (rule == null)
            {
                return null;
            }

            return new PoseCategoryInferRule
            {
                RuleId = rule.RuleId,
                TargetCategory = rule.TargetCategory,
                Priority = rule.Priority,
                Enabled = rule.Enabled != false,
                RequiredAll = (rule.RequiredAll ?? new string[0]).ToArray(),
                RequiredAny = (rule.RequiredAny ?? new string[0]).ToArray(),
                ExcludeAny = (rule.ExcludeAny ?? new string[0]).ToArray()
            };
        }

        private static PoseScoreRulesFile DeserializePoseScoreRulesFile(string path)
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidDataException("pose_score_rules.json is empty");
            }

            var serializer = new DataContractJsonSerializer(
                typeof(PoseScoreRulesFile),
                new DataContractJsonSerializerSettings
                {
                    UseSimpleDictionaryFormat = true
                });
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            using (var ms = new MemoryStream(bytes))
            {
                PoseScoreRulesFile file = serializer.ReadObject(ms) as PoseScoreRulesFile;
                if (file == null)
                {
                    throw new InvalidDataException("pose_score_rules.json parse returned null");
                }

                return file;
            }
        }

        private static void SavePoseScoreRulesFile(string path, PoseScoreRulesFile file)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var serializer = new DataContractJsonSerializer(
                typeof(PoseScoreRulesFile),
                new DataContractJsonSerializerSettings
                {
                    UseSimpleDictionaryFormat = true
                });
            using (var ms = new MemoryStream())
            {
                serializer.WriteObject(ms, file);
                string json = Encoding.UTF8.GetString(ms.ToArray());
                File.WriteAllText(path, json, Utf8NoBom);
            }
        }

        private void SaveCurrentPoseScoreRulesToFile(string reason)
        {
            string path = _poseScoreRulesFilePath;
            if (string.IsNullOrWhiteSpace(path))
            {
                path = Path.Combine(PluginDir ?? string.Empty, PoseScoreRulesFileName);
            }

            try
            {
                var file = new PoseScoreRulesFile
                {
                    Version = PoseScoreRulesCurrentVersion,
                    Enabled = _poseChangeEnabled,
                    SimpleModeEnabled = _poseSimpleModeEnabled,
                    SimpleModeTriggerKeywords = _poseSimpleModeTriggerKeywords,
                    RulesEnabled = _poseRulesEnabled,
                    InferRulesEnabled = _poseInferRulesEnabled,
                    ScoreBase = _poseScoreBase,
                    AdoptThreshold = _poseAdoptThreshold,
                    ForceThreshold = _poseForceThreshold,
                    CategoryEnabled = new Dictionary<string, bool>(_poseCategoryEnabled, StringComparer.Ordinal),
                    Rules = (_poseKeywordScoreRules ?? new List<PoseKeywordScoreRule>())
                        .Where(r => r != null)
                        .Select(ClonePoseScoreRule)
                        .ToList(),
                    InferRules = (_poseCategoryInferRules ?? new List<PoseCategoryInferRule>())
                        .Where(r => r != null)
                        .Select(ClonePoseInferRule)
                        .ToList()
                };

                SavePoseScoreRulesFile(path, file);
                Log("[pose-score] saved by " + reason);
            }
            catch (Exception ex)
            {
                LogWarn("[pose-score] save failed reason=" + reason + " message=" + ex.Message);
            }
        }

        private void EnsurePoseClassificationFilesFromProc(HSceneProc proc)
        {
            if (proc == null || string.IsNullOrWhiteSpace(PluginDir))
            {
                return;
            }

            EnsurePoseListFileFromProc(proc);

            bool created = false;
            created |= EnsureSinglePoseClassificationFile(proc, PoseSonyuClassifiedFileName, classifyType: "sonyu");
            created |= EnsureSinglePoseClassificationFile(proc, PoseHoushiClassifiedFileName, classifyType: "houshi");
            created |= EnsureSinglePoseClassificationFile(proc, PoseMasturbationClassifiedFileName, classifyType: "masturbation");

            if (created)
            {
                LoadPoseCategoryEntries();
            }
        }

        private bool EnsurePoseListFileFromProc(HSceneProc proc)
        {
            if (proc == null || string.IsNullOrWhiteSpace(PluginDir))
            {
                return false;
            }

            string path = Path.Combine(PluginDir, PoseListFileName);
            if (File.Exists(path))
            {
                return false;
            }

            try
            {
                var lists = LstUseAnimInfoField?.GetValue(proc) as List<HSceneProc.AnimationListInfo>[];
                if (lists == null)
                {
                    LogWarn("[pose-list] auto-create skipped (lstUseAnimInfo is null)");
                    return false;
                }

                List<PoseListItem> poses = BuildPoseListItems(lists);
                SavePoseList(path, poses);
                Log($"[pose-list] auto-created: {path} entries={poses.Count}");
                return true;
            }
            catch (Exception ex)
            {
                LogWarn("[pose-list] auto-create failed: " + ex.Message);
                return false;
            }
        }

        private bool EnsureSinglePoseClassificationFile(HSceneProc proc, string fileName, string classifyType)
        {
            if (proc == null || string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(PluginDir) || string.IsNullOrWhiteSpace(classifyType))
            {
                return false;
            }

            string path = Path.Combine(PluginDir, fileName);
            if (File.Exists(path))
            {
                return false;
            }

            try
            {
                var lists = LstUseAnimInfoField?.GetValue(proc) as List<HSceneProc.AnimationListInfo>[];
                if (lists == null)
                {
                    LogWarn("[pose-classify] auto-create skipped (lstUseAnimInfo is null): " + fileName);
                    return false;
                }

                Dictionary<string, List<PoseClassificationItem>> categories;
                if (string.Equals(classifyType, "sonyu", StringComparison.Ordinal))
                {
                    categories = BuildAutoSonyuPoseCategories(lists);
                }
                else if (string.Equals(classifyType, "houshi", StringComparison.Ordinal))
                {
                    categories = BuildAutoHoushiPoseCategories(lists);
                }
                else if (string.Equals(classifyType, "masturbation", StringComparison.Ordinal))
                {
                    categories = BuildAutoMasturbationPoseCategories(lists);
                }
                else
                {
                    LogWarn("[pose-classify] auto-create skipped (unknown classifyType): " + classifyType);
                    return false;
                }

                SavePoseClassification(path, categories);
                int entryCount = categories.Sum(x => x.Value != null ? x.Value.Count : 0);
                Log($"[pose-classify] auto-created: {path} categories={categories.Count} entries={entryCount}");
                return true;
            }
            catch (Exception ex)
            {
                LogWarn("[pose-classify] auto-create failed file=" + fileName + " message=" + ex.Message);
                return false;
            }
        }

        private static Dictionary<string, List<PoseClassificationItem>> BuildAutoSonyuPoseCategories(List<HSceneProc.AnimationListInfo>[] lists)
        {
            var categories = CreateCategoryMap(SonyuCategoryNames);
            if (lists == null)
            {
                return categories;
            }

            for (int mode = 0; mode < lists.Length; mode++)
            {
                if (!IsSonyuMode(mode))
                {
                    continue;
                }

                var list = lists[mode];
                if (list == null)
                {
                    continue;
                }

                for (int i = 0; i < list.Count; i++)
                {
                    var info = list[i];
                    if (info == null || string.IsNullOrWhiteSpace(info.nameAnimation))
                    {
                        continue;
                    }

                    string category = ClassifySonyuPoseCategory(info.nameAnimation);
                    AddPoseClassificationItem(categories, category, info.nameAnimation, mode);
                }
            }

            return categories;
        }

        private static List<PoseListItem> BuildPoseListItems(List<HSceneProc.AnimationListInfo>[] lists)
        {
            var results = new List<PoseListItem>();
            if (lists == null)
            {
                return results;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int mode = 0; mode < lists.Length; mode++)
            {
                var list = lists[mode];
                if (list == null)
                {
                    continue;
                }

                for (int i = 0; i < list.Count; i++)
                {
                    var info = list[i];
                    if (info == null || string.IsNullOrWhiteSpace(info.nameAnimation))
                    {
                        continue;
                    }

                    string key = mode + "|" + info.id + "|" + info.nameAnimation;
                    if (!seen.Add(key))
                    {
                        continue;
                    }

                    results.Add(new PoseListItem
                    {
                        Id = info.id,
                        Mode = info.mode.ToString(),
                        ModeInt = mode,
                        NameAnimation = info.nameAnimation
                    });
                }
            }

            return results
                .OrderBy(x => x.ModeInt)
                .ThenBy(x => x.Id)
                .ThenBy(x => x.NameAnimation, StringComparer.Ordinal)
                .ToList();
        }

        private static Dictionary<string, List<PoseClassificationItem>> BuildAutoHoushiPoseCategories(List<HSceneProc.AnimationListInfo>[] lists)
        {
            var categories = CreateCategoryMap(HoushiCategoryNames);
            if (lists == null)
            {
                return categories;
            }

            for (int mode = 0; mode < lists.Length; mode++)
            {
                if (!IsHoushiMode(mode))
                {
                    continue;
                }

                var list = lists[mode];
                if (list == null)
                {
                    continue;
                }

                for (int i = 0; i < list.Count; i++)
                {
                    var info = list[i];
                    if (info == null || string.IsNullOrWhiteSpace(info.nameAnimation))
                    {
                        continue;
                    }

                    string category = ClassifyHoushiPoseCategory(info.nameAnimation);
                    AddPoseClassificationItem(categories, category, info.nameAnimation, mode);
                }
            }

            return categories;
        }

        private static Dictionary<string, List<PoseClassificationItem>> BuildAutoMasturbationPoseCategories(List<HSceneProc.AnimationListInfo>[] lists)
        {
            var categories = CreateCategoryMap(MasturbationCategoryNames);
            if (lists == null)
            {
                return categories;
            }

            const string categoryName = "オナニー系";
            for (int mode = 0; mode < lists.Length; mode++)
            {
                if (!IsMasturbationMode(mode))
                {
                    continue;
                }

                var list = lists[mode];
                if (list == null)
                {
                    continue;
                }

                for (int i = 0; i < list.Count; i++)
                {
                    var info = list[i];
                    if (info == null || string.IsNullOrWhiteSpace(info.nameAnimation))
                    {
                        continue;
                    }

                    AddPoseClassificationItem(categories, categoryName, info.nameAnimation, mode);
                }
            }

            return categories;
        }

        private static Dictionary<string, List<PoseClassificationItem>> CreateCategoryMap(string[] categoryNames)
        {
            var map = new Dictionary<string, List<PoseClassificationItem>>(StringComparer.Ordinal);
            if (categoryNames == null)
            {
                return map;
            }

            for (int i = 0; i < categoryNames.Length; i++)
            {
                string category = categoryNames[i];
                if (string.IsNullOrWhiteSpace(category))
                {
                    continue;
                }

                if (!map.ContainsKey(category))
                {
                    map[category] = new List<PoseClassificationItem>();
                }
            }

            return map;
        }

        private static bool IsSonyuMode(int mode)
        {
            return mode == 2 || mode == 7 || mode == 9;
        }

        private static bool IsHoushiMode(int mode)
        {
            return mode == 1 || mode == 6 || mode == 8;
        }

        private static bool IsMasturbationMode(int mode)
        {
            return mode == 3;
        }

        private static string ClassifySonyuPoseCategory(string poseName)
        {
            string name = poseName ?? string.Empty;

            if (ContainsAnyCategoryKeyword(name, "逆騎乗", "背面騎乗", "後ろ向き", "Reverse Cowgirl"))
            {
                return "背面騎乗位系";
            }
            if (ContainsAnyCategoryKeyword(name, "騎乗", "Cowgirl", "またが", "跨"))
            {
                return "騎乗位系";
            }
            if (ContainsAnyCategoryKeyword(name, "側位", "横", "Side", "Princess Hug"))
            {
                return "測位系";
            }
            if (ContainsAnyCategoryKeyword(name, "座位", "椅子", "床", "正座", "膝立て", "Sitting"))
            {
                return "座位系";
            }

            bool standing = ContainsAnyCategoryKeyword(name, "立ち", "立位", "Standing", "駅弁", "Wall");
            bool back = ContainsAnyCategoryKeyword(name, "バック", "後背", "後ろ", "doggy", "Doggystyle", "from behind", "フェンス");
            if (standing && back)
            {
                return "立後背位系";
            }
            if (back)
            {
                return "後背位系";
            }
            if (standing)
            {
                return "立位系";
            }

            return "正常位系";
        }

        private static string ClassifyHoushiPoseCategory(string poseName)
        {
            string name = poseName ?? string.Empty;

            if (ContainsAnyCategoryKeyword(name, "69", "シックスナイン", "sixty"))
            {
                return "69系";
            }
            if (ContainsAnyCategoryKeyword(name, "フェラ", "口", "oral", "blow", "咥", "しゃぶ"))
            {
                return "フェラ系";
            }
            if (ContainsAnyCategoryKeyword(name, "パイズリ", "boob", "titty", "乳"))
            {
                return "パイズリ系";
            }
            if (ContainsAnyCategoryKeyword(name, "クンニ", "cunni", "舐"))
            {
                return "クンニ系";
            }
            if (ContainsAnyCategoryKeyword(name, "顔面騎乗", "face sit"))
            {
                return "顔面騎乗系";
            }
            if (ContainsAnyCategoryKeyword(name, "足コキ", "leg", "foot"))
            {
                return "足コキ系";
            }
            if (ContainsAnyCategoryKeyword(name, "手コキ", "hand", "手"))
            {
                return "手コキ系";
            }

            return "キス・愛撫系";
        }

        private static bool ContainsAnyCategoryKeyword(string text, params string[] keywords)
        {
            if (string.IsNullOrWhiteSpace(text) || keywords == null || keywords.Length <= 0)
            {
                return false;
            }

            for (int i = 0; i < keywords.Length; i++)
            {
                string keyword = keywords[i];
                if (ContainsKeyword(text, keyword))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddPoseClassificationItem(
            Dictionary<string, List<PoseClassificationItem>> categories,
            string category,
            string nameAnimation,
            int modeInt)
        {
            if (categories == null || string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(nameAnimation))
            {
                return;
            }

            if (!categories.TryGetValue(category, out var list))
            {
                list = new List<PoseClassificationItem>();
                categories[category] = list;
            }

            bool exists = list.Any(x =>
                x != null &&
                x.ModeInt == modeInt &&
                string.Equals(x.NameAnimation, nameAnimation, StringComparison.Ordinal));
            if (exists)
            {
                return;
            }

            list.Add(new PoseClassificationItem
            {
                NameAnimation = nameAnimation,
                ModeInt = modeInt
            });
        }

        private static void SavePoseClassification(string path, Dictionary<string, List<PoseClassificationItem>> categories)
        {
            var root = new PoseClassificationFile
            {
                Categories = categories ?? new Dictionary<string, List<PoseClassificationItem>>(StringComparer.Ordinal)
            };

            var serializer = new DataContractJsonSerializer(
                typeof(PoseClassificationFile),
                new DataContractJsonSerializerSettings
                {
                    UseSimpleDictionaryFormat = true
                });

            using (var ms = new MemoryStream())
            {
                serializer.WriteObject(ms, root);
                string json = Encoding.UTF8.GetString(ms.ToArray());
                File.WriteAllText(path, json, Utf8NoBom);
            }
        }

        private static void SavePoseList(string path, List<PoseListItem> poses)
        {
            var root = new PoseListFile
            {
                GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Source = "HSceneProc.lstUseAnimInfo",
                Poses = poses ?? new List<PoseListItem>()
            };

            var serializer = new DataContractJsonSerializer(
                typeof(PoseListFile),
                new DataContractJsonSerializerSettings
                {
                    UseSimpleDictionaryFormat = true
                });

            using (var ms = new MemoryStream())
            {
                serializer.WriteObject(ms, root);
                string json = Encoding.UTF8.GetString(ms.ToArray());
                File.WriteAllText(path, json, Utf8NoBom);
            }
        }

        private void LoadPoseCategoryEntries()
        {
            _poseEntriesByCategory.Clear();
            _poseNameAliasesByCanonical.Clear();
            int added = 0;
            added += AppendPoseCategoriesFromFile(PoseSonyuClassifiedFileName);
            added += AppendPoseCategoriesFromFile(PoseHoushiClassifiedFileName);
            added += AppendPoseCategoriesFromFile(PoseMasturbationClassifiedFileName);
            int aliasCount = LoadPoseNameAliasesFromTranslatedFile();
            Log($"[pose-classify] loaded categories={_poseEntriesByCategory.Count} entries={added} aliasKeys={_poseNameAliasesByCanonical.Count} aliases={aliasCount}");
        }

        private int LoadPoseNameAliasesFromTranslatedFile()
        {
            if (string.IsNullOrWhiteSpace(PluginDir))
            {
                return 0;
            }

            string path = Path.Combine(PluginDir, PoseListTranslatedFileName);
            if (!File.Exists(path))
            {
                LogWarn("[pose-alias] file not found: " + path);
                return 0;
            }

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                {
                    LogWarn("[pose-alias] file empty: " + path);
                    return 0;
                }

                var serializer = new DataContractJsonSerializer(
                    typeof(PoseTranslatedListFile),
                    new DataContractJsonSerializerSettings
                    {
                        UseSimpleDictionaryFormat = true
                    });
                PoseTranslatedListFile root;
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    root = serializer.ReadObject(ms) as PoseTranslatedListFile;
                }

                if (root?.Poses == null || root.Poses.Count <= 0)
                {
                    LogWarn("[pose-alias] poses empty: " + path);
                    return 0;
                }

                int added = 0;
                foreach (PoseTranslatedListItem pose in root.Poses)
                {
                    if (pose == null)
                    {
                        continue;
                    }

                    string canonical = (pose.NameAnimationOriginal ?? string.Empty).Trim();
                    string alias = (pose.NameAnimation ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(canonical) || string.IsNullOrWhiteSpace(alias))
                    {
                        continue;
                    }

                    if (string.Equals(canonical, alias, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!_poseNameAliasesByCanonical.TryGetValue(canonical, out List<string> aliases))
                    {
                        aliases = new List<string>();
                        _poseNameAliasesByCanonical[canonical] = aliases;
                    }

                    if (aliases.Any(x => string.Equals(x, alias, StringComparison.Ordinal)))
                    {
                        continue;
                    }

                    aliases.Add(alias);
                    added++;
                }

                return added;
            }
            catch (Exception ex)
            {
                LogWarn("[pose-alias] load failed: " + ex.Message);
                return 0;
            }
        }

        private int AppendPoseCategoriesFromFile(string fileName)
        {
            if (string.IsNullOrWhiteSpace(PluginDir) || string.IsNullOrWhiteSpace(fileName))
            {
                return 0;
            }

            string path = Path.Combine(PluginDir, fileName);
            if (!File.Exists(path))
            {
                LogWarn("[pose-classify] file not found: " + path);
                return 0;
            }

            try
            {
                PoseClassificationFile root = DeserializePoseClassification(path);
                if (root == null)
                {
                    LogWarn("[pose-classify] root parse null: " + fileName);
                    return 0;
                }

                if (root.Categories == null)
                {
                    LogWarn("[pose-classify] categories parse null: " + fileName);
                    return 0;
                }

                if (root.Categories.Count <= 0)
                {
                    LogWarn("[pose-classify] categories empty: " + fileName);
                    return 0;
                }

                int added = 0;
                foreach (var pair in root.Categories)
                {
                    string category = pair.Key;
                    if (string.IsNullOrWhiteSpace(category) || pair.Value == null)
                    {
                        continue;
                    }

                    if (!_poseEntriesByCategory.TryGetValue(category, out var list))
                    {
                        list = new List<PoseCategoryEntry>();
                        _poseEntriesByCategory[category] = list;
                    }

                    foreach (var item in pair.Value)
                    {
                        if (item == null || string.IsNullOrWhiteSpace(item.NameAnimation))
                        {
                            continue;
                        }

                        bool exists = list.Any(x =>
                            x != null &&
                            x.ModeInt == item.ModeInt &&
                            string.Equals(x.NameAnimation, item.NameAnimation, StringComparison.Ordinal));
                        if (exists)
                        {
                            continue;
                        }

                        list.Add(new PoseCategoryEntry
                        {
                            NameAnimation = item.NameAnimation,
                            ModeInt = item.ModeInt
                        });
                        added++;
                    }
                }

                return added;
            }
            catch (Exception ex)
            {
                LogWarn("[pose-classify] load failed file=" + fileName + " message=" + ex.Message);
                return 0;
            }
        }

        private static PoseClassificationFile DeserializePoseClassification(string path)
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            var serializer = new DataContractJsonSerializer(
                typeof(PoseClassificationFile),
                new DataContractJsonSerializerSettings
                {
                    UseSimpleDictionaryFormat = true
                });
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            using (var ms = new MemoryStream(bytes))
            {
                return serializer.ReadObject(ms) as PoseClassificationFile;
            }
        }


        private static string BuildPoseEntryKey(PoseCategoryEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.NameAnimation))
            {
                return string.Empty;
            }

            return $"{entry.ModeInt}|{entry.NameAnimation}";
        }

        private bool TryPickPoseFromText(string text, out string poseName, out int poseMode, out string poseCategory, out int poseMatchIndex)
        {
            poseName = null;
            poseMode = -1;
            poseCategory = null;
            poseMatchIndex = -1;

            if (string.IsNullOrWhiteSpace(text) || _poseEntriesByCategory.Count <= 0)
            {
                return false;
            }

            if (!_poseChangeEnabled)
            {
                return false;
            }

            if (_poseSimpleModeEnabled)
            {
                bool picked = TryPickPoseFromTextSimple(text, out poseName, out poseMode, out poseCategory, out poseMatchIndex);
                if (!picked)
                {
                    Log($"[pose-simple] no-match triggers='{_poseSimpleModeTriggerKeywords}'");
                }
                return picked;
            }

            int bestAliasLen = -1;
            string bestCategory = null;

            foreach (var pair in _poseEntriesByCategory)
            {

                string[] aliases = ResolvePoseCategoryAliases(pair.Key);
                foreach (string alias in aliases)
                {
                    if (string.IsNullOrWhiteSpace(alias))
                    {
                        continue;
                    }

                    if (text.IndexOf(alias, StringComparison.Ordinal) < 0)
                    {
                        continue;
                    }

                    if (alias.Length > bestAliasLen)
                    {
                        bestAliasLen = alias.Length;
                        bestCategory = pair.Key;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(bestCategory) &&
                TryResolveInferredPoseCategory(text, out string inferredCategory, out string inferredRuleId))
            {
                bestCategory = inferredCategory;
                bestAliasLen = 0;
                Log($"[pose] category inferred: {bestCategory} (rule={inferredRuleId})");
            }

            if (string.IsNullOrWhiteSpace(bestCategory) &&
                TryPickPoseFromGlobalTokenScore(text, out var globalCategory, out var globalPose, out var globalMatch))
            {
                poseCategory = globalCategory;
                poseName = globalPose?.NameAnimation;
                poseMode = globalPose != null ? globalPose.ModeInt : -1;
                if (!string.IsNullOrWhiteSpace(poseName))
                {
                    poseMatchIndex = FindPoseMatchIndexFromText(text, poseName, poseCategory);
                    string level = globalMatch != null && globalMatch.Score >= _poseForceThreshold ? "force" : "prefer";
                    Log($"[pose] scored-{level} category+pose inferred category={poseCategory} rule={globalMatch?.Rule?.RuleId} score={globalMatch?.Score} pose={poseName}");
                    return true;
                }
            }

            if (string.IsNullOrWhiteSpace(bestCategory))
            {
                return false;
            }

            if (!_poseEntriesByCategory.TryGetValue(bestCategory, out var entries) || entries == null || entries.Count <= 0)
            {
                return false;
            }

            PoseCategoryEntry selected = TryPickPreferredPoseEntry(bestCategory, text, entries);
            if (selected == null)
            {
                int index;
                lock (_random)
                {
                    index = _random.Next(entries.Count);
                }

                selected = entries[index];
            }

            poseName = selected?.NameAnimation;
            poseMode = selected != null ? selected.ModeInt : -1;
            poseCategory = bestCategory;
            poseMatchIndex = FindPoseMatchIndexFromText(text, poseName, poseCategory);

            return !string.IsNullOrWhiteSpace(poseName);
        }

        private bool TryPickPoseFromTextSimple(string text, out string poseName, out int poseMode, out string poseCategory, out int poseMatchIndex)
        {
            poseName = null;
            poseMode = -1;
            poseCategory = null;
            poseMatchIndex = -1;

            if (string.IsNullOrWhiteSpace(text) || _poseEntriesByCategory.Count <= 0)
            {
                return false;
            }

            string[] triggerKeywords = GetPoseSimpleModeTriggerKeywords();
            if (triggerKeywords.Length <= 0)
            {
                return false;
            }

            List<TextLineSpan> lines = SplitTextLinesWithOffsets(text);
            foreach (TextLineSpan lineSpan in lines)
            {
                string rawLine = lineSpan?.Line ?? string.Empty;
                int trimStart = 0;
                while (trimStart < rawLine.Length && char.IsWhiteSpace(rawLine[trimStart]))
                {
                    trimStart++;
                }

                if (trimStart >= rawLine.Length)
                {
                    continue;
                }

                int trimEnd = rawLine.Length - 1;
                while (trimEnd >= trimStart && char.IsWhiteSpace(rawLine[trimEnd]))
                {
                    trimEnd--;
                }

                if (trimEnd < trimStart)
                {
                    continue;
                }

                string line = rawLine.Substring(trimStart, trimEnd - trimStart + 1);
                int lineStartAbs = lineSpan.StartIndex + trimStart;

                bool hasAnyTrigger = false;
                bool hasBest = false;
                bool bestIsExact = false;
                int bestDistance = int.MaxValue;
                int bestMatchLength = -1;
                int bestMatchStart = -1;
                int bestMatchStartAbs = -1;
                int bestTriggerIndexAbs = -1;
                string bestTrigger = null;
                string bestCategory = null;
                string bestAlias = null;
                PoseCategoryEntry bestEntry = null;

                foreach (string trigger in triggerKeywords)
                {
                    if (string.IsNullOrWhiteSpace(trigger))
                    {
                        continue;
                    }

                    int searchIndex = 0;
                    while (searchIndex < line.Length)
                    {
                        int triggerIndex = line.IndexOf(trigger, searchIndex, StringComparison.Ordinal);
                        if (triggerIndex < 0)
                        {
                            break;
                        }

                        hasAnyTrigger = true;
                        searchIndex = triggerIndex + Math.Max(1, trigger.Length);

                        if (TryPickExactPoseBeforeIndex(
                                line,
                                triggerIndex,
                                out string exactCategory,
                                out PoseCategoryEntry exactEntry,
                                out int exactDistance,
                                out int exactMatchStart,
                                out int exactMatchLength))
                        {
                            if (ShouldReplaceSimplePoseCandidate(
                                    hasBest,
                                    bestDistance,
                                    bestIsExact,
                                    bestMatchLength,
                                    bestMatchStart,
                                    exactDistance,
                                    true,
                                    exactMatchLength,
                                    exactMatchStart))
                            {
                                hasBest = true;
                                bestIsExact = true;
                                bestDistance = exactDistance;
                                bestMatchLength = exactMatchLength;
                                bestMatchStart = exactMatchStart;
                                bestMatchStartAbs = lineStartAbs + exactMatchStart;
                                bestTriggerIndexAbs = lineStartAbs + triggerIndex;
                                bestTrigger = trigger;
                                bestCategory = exactCategory;
                                bestAlias = null;
                                bestEntry = exactEntry;
                            }
                        }

                        if (TryPickCategoryRandomPoseBeforeIndex(
                                line,
                                triggerIndex,
                                out string randomCategory,
                                out string matchedAlias,
                                out PoseCategoryEntry randomEntry,
                                out int randomDistance,
                                out int randomAliasStart,
                                out int randomAliasLength))
                        {
                            if (ShouldReplaceSimplePoseCandidate(
                                    hasBest,
                                    bestDistance,
                                    bestIsExact,
                                    bestMatchLength,
                                    bestMatchStart,
                                    randomDistance,
                                    false,
                                    randomAliasLength,
                                    randomAliasStart))
                            {
                                hasBest = true;
                                bestIsExact = false;
                                bestDistance = randomDistance;
                                bestMatchLength = randomAliasLength;
                                bestMatchStart = randomAliasStart;
                                bestMatchStartAbs = lineStartAbs + randomAliasStart;
                                bestTriggerIndexAbs = lineStartAbs + triggerIndex;
                                bestTrigger = trigger;
                                bestCategory = randomCategory;
                                bestAlias = matchedAlias;
                                bestEntry = randomEntry;
                            }
                        }
                    }
                }

                if (!hasAnyTrigger)
                {
                    continue;
                }

                if (!hasBest || bestEntry == null || string.IsNullOrWhiteSpace(bestCategory))
                {
                    string noPickPreview = line.Length > 80 ? line.Substring(0, 80) : line;
                    Log($"[pose-simple] trigger-only line='{noPickPreview}' result=no-pose-before-trigger");
                    continue;
                }

                poseCategory = bestCategory;
                poseName = bestEntry.NameAnimation;
                poseMode = bestEntry.ModeInt;
                poseMatchIndex = bestMatchStartAbs >= 0 ? bestMatchStartAbs : bestTriggerIndexAbs;

                string preview = line.Length > 80 ? line.Substring(0, 80) : line;
                if (bestIsExact)
                {
                    Log($"[pose-simple] exact-nearest line='{preview}' trigger='{bestTrigger}' triggerPos={bestTriggerIndexAbs} posePos={poseMatchIndex} distance={bestDistance} category='{poseCategory}' pose='{poseName}' mode={poseMode}");
                }
                else
                {
                    Log($"[pose-simple] group-nearest line='{preview}' trigger='{bestTrigger}' triggerPos={bestTriggerIndexAbs} posePos={poseMatchIndex} distance={bestDistance} category='{poseCategory}' alias='{bestAlias}' pose='{poseName}' mode={poseMode}");
                }

                return true;
            }

            return false;
        }

        private static bool ShouldReplaceSimplePoseCandidate(
            bool hasBest,
            int bestDistance,
            bool bestIsExact,
            int bestMatchLength,
            int bestMatchStart,
            int candidateDistance,
            bool candidateIsExact,
            int candidateMatchLength,
            int candidateMatchStart)
        {
            if (!hasBest)
            {
                return true;
            }

            if (candidateDistance < bestDistance)
            {
                return true;
            }

            if (candidateDistance > bestDistance)
            {
                return false;
            }

            if (candidateIsExact && !bestIsExact)
            {
                return true;
            }

            if (!candidateIsExact && bestIsExact)
            {
                return false;
            }

            if (candidateMatchLength > bestMatchLength)
            {
                return true;
            }

            if (candidateMatchLength < bestMatchLength)
            {
                return false;
            }

            return candidateMatchStart > bestMatchStart;
        }

        private bool TryPickExactPoseBeforeIndex(
            string line,
            int triggerIndex,
            out string category,
            out PoseCategoryEntry selected,
            out int distance,
            out int matchStart,
            out int matchLength)
        {
            category = null;
            selected = null;
            distance = int.MaxValue;
            matchStart = -1;
            matchLength = -1;
            if (string.IsNullOrWhiteSpace(line) || triggerIndex <= 0)
            {
                return false;
            }

            foreach (var pair in _poseEntriesByCategory)
            {
                if (pair.Value == null || pair.Value.Count <= 0)
                {
                    continue;
                }

                foreach (PoseCategoryEntry entry in pair.Value)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.NameAnimation))
                    {
                        continue;
                    }

                    foreach (string matchName in EnumeratePoseMatchNames(entry))
                    {
                        if (string.IsNullOrWhiteSpace(matchName))
                        {
                            continue;
                        }

                        int start = FindLastIndexBefore(
                            line,
                            matchName,
                            triggerIndex,
                            StringComparison.OrdinalIgnoreCase);
                        if (start < 0)
                        {
                            continue;
                        }

                        int len = matchName.Length;
                        int d = triggerIndex - (start + len);
                        if (d < 0)
                        {
                            continue;
                        }

                        if (selected == null || d < distance || (d == distance && len > matchLength))
                        {
                            category = pair.Key;
                            selected = entry;
                            distance = d;
                            matchStart = start;
                            matchLength = len;
                        }
                    }
                }
            }

            return selected != null && !string.IsNullOrWhiteSpace(category);
        }

        private bool TryPickCategoryRandomPoseBeforeIndex(
            string line,
            int triggerIndex,
            out string category,
            out string matchedAlias,
            out PoseCategoryEntry selected,
            out int distance,
            out int aliasStart,
            out int aliasLength)
        {
            category = null;
            matchedAlias = null;
            selected = null;
            distance = int.MaxValue;
            aliasStart = -1;
            aliasLength = -1;
            if (string.IsNullOrWhiteSpace(line) || triggerIndex <= 0)
            {
                return false;
            }

            foreach (var pair in _poseEntriesByCategory)
            {
                if (!IsPoseCategoryEnabled(pair.Key) || pair.Value == null || pair.Value.Count <= 0)
                {
                    continue;
                }

                string[] aliases = ResolvePoseCategoryAliases(pair.Key);
                foreach (string alias in aliases)
                {
                    if (string.IsNullOrWhiteSpace(alias))
                    {
                        continue;
                    }

                    int start = FindLastIndexBefore(
                        line,
                        alias,
                        triggerIndex,
                        StringComparison.Ordinal);
                    if (start < 0)
                    {
                        continue;
                    }

                    int len = alias.Length;
                    int d = triggerIndex - (start + len);
                    if (d < 0)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(category) || d < distance || (d == distance && len > aliasLength))
                    {
                        category = pair.Key;
                        matchedAlias = alias;
                        distance = d;
                        aliasStart = start;
                        aliasLength = len;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(category))
            {
                return false;
            }

            if (!_poseEntriesByCategory.TryGetValue(category, out var entries) || entries == null || entries.Count <= 0)
            {
                return false;
            }

            lock (_random)
            {
                selected = entries[_random.Next(entries.Count)];
            }

            return selected != null;
        }

        private static int FindLastIndexBefore(
            string line,
            string token,
            int endExclusive,
            StringComparison comparisonType)
        {
            if (string.IsNullOrEmpty(line) || string.IsNullOrEmpty(token) || endExclusive <= 0)
            {
                return -1;
            }

            int maxEnd = Math.Min(endExclusive, line.Length);
            int searchIndex = 0;
            int last = -1;
            while (searchIndex < maxEnd)
            {
                int index = line.IndexOf(token, searchIndex, comparisonType);
                if (index < 0 || index >= maxEnd)
                {
                    break;
                }

                last = index;
                searchIndex = index + 1;
            }

            return last;
        }

        private static List<TextLineSpan> SplitTextLinesWithOffsets(string text)
        {
            var lines = new List<TextLineSpan>();
            if (string.IsNullOrEmpty(text))
            {
                return lines;
            }

            int start = 0;
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (c != '\r' && c != '\n')
                {
                    i++;
                    continue;
                }

                int len = i - start;
                if (len > 0)
                {
                    lines.Add(new TextLineSpan
                    {
                        Line = text.Substring(start, len),
                        StartIndex = start
                    });
                }

                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                i++;
                start = i;
            }

            if (start < text.Length)
            {
                lines.Add(new TextLineSpan
                {
                    Line = text.Substring(start),
                    StartIndex = start
                });
            }

            return lines;
        }

        private List<TimedCoordItem> FindCoordMatchesFromText(
            string text,
            string[] coordTriggers,
            int main)
        {
            var results = new List<TimedCoordItem>();
            if (string.IsNullOrWhiteSpace(text) || coordTriggers == null || coordTriggers.Length <= 0)
            {
                return results;
            }

            HSceneProc coordProc = CurrentProc;
            ChaControl coordFemale = coordProc != null ? ResolveFemale(coordProc, main) : null;
            if (coordFemale == null)
            {
                return results;
            }

            var coordSlots = coordFemale.chaFile?.coordinate;
            if (coordSlots == null)
            {
                return results;
            }

            MonoBehaviour moCtrl = null;
            MethodInfo moGetName = null;
            try
            {
                moCtrl = coordFemale.gameObject.GetComponents<MonoBehaviour>()
                    .FirstOrDefault(c => c.GetType().Name == "MoreOutfitsController");
                if (moCtrl != null)
                {
                    moGetName = moCtrl.GetType().GetMethod("GetCoodinateName",
                        BindingFlags.Public | BindingFlags.Instance);
                }
            }
            catch { }

            foreach (TextLineSpan lineSpan in SplitTextLinesWithOffsets(text))
            {
                string line = lineSpan?.Line ?? string.Empty;
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                List<VideoTriggerHit> hits = FindVideoTriggerHits(line, coordTriggers);
                if (hits == null || hits.Count <= 0)
                {
                    continue;
                }

                foreach (VideoTriggerHit hit in hits.OrderByDescending(x => x.Index))
                {
                    string bestName = null;
                    int bestPos = -1;
                    int bestDistance = int.MaxValue;
                    int bestLength = -1;

                    for (int i = 0; i < coordSlots.Length; i++)
                    {
                        string slotName = GetCoordSlotName(i, moCtrl, moGetName);
                        if (string.IsNullOrWhiteSpace(slotName))
                        {
                            continue;
                        }

                        int slotPos = FindLastIndexBefore(line, slotName, hit.Index, StringComparison.OrdinalIgnoreCase);
                        if (slotPos < 0)
                        {
                            continue;
                        }

                        int distance = hit.Index - (slotPos + slotName.Length);
                        if (distance < 0)
                        {
                            continue;
                        }

                        if (distance < bestDistance
                            || (distance == bestDistance && slotPos > bestPos)
                            || (distance == bestDistance && slotPos == bestPos && slotName.Length > bestLength))
                        {
                            bestName = slotName;
                            bestPos = slotPos;
                            bestDistance = distance;
                            bestLength = slotName.Length;
                        }
                    }

                    if (!string.IsNullOrEmpty(bestName))
                    {
                        results.Add(new TimedCoordItem
                        {
                            CoordName = bestName,
                            MatchIndex = lineSpan.StartIndex + bestPos,
                            TriggerKeyword = hit.Keyword
                        });
                    }
                }
            }

            return results
                .OrderBy(x => x.MatchIndex)
                .ThenBy(x => x.CoordName, StringComparer.Ordinal)
                .ToList();
        }

        private static float ComputeActionDelaySecondsByTextPosition(string text, float totalDelaySeconds, int matchIndex)
        {
            float total = Mathf.Max(0f, totalDelaySeconds);
            if (total <= 0f || string.IsNullOrEmpty(text))
            {
                return 0f;
            }

            int maxIndex = Math.Max(0, text.Length - 1);
            int index = Mathf.Clamp(matchIndex, 0, maxIndex);
            float ratio = maxIndex > 0 ? index / (float)maxIndex : 0f;
            return total * Mathf.Clamp01(ratio);
        }

        private static int FindFirstKeywordIndex(string text, string[] keywords, StringComparison comparisonType)
        {
            if (string.IsNullOrEmpty(text) || keywords == null || keywords.Length <= 0)
            {
                return -1;
            }

            int best = -1;
            foreach (string keyword in keywords)
            {
                if (string.IsNullOrWhiteSpace(keyword))
                {
                    continue;
                }

                int index = text.IndexOf(keyword, comparisonType);
                if (index < 0)
                {
                    continue;
                }

                if (best < 0 || index < best)
                {
                    best = index;
                }
            }

            return best;
        }

        private int FindPoseMatchIndexFromText(string text, string poseName, string poseCategory)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return -1;
            }

            if (!string.IsNullOrWhiteSpace(poseName))
            {
                int poseIndex = text.IndexOf(poseName, StringComparison.OrdinalIgnoreCase);
                if (poseIndex >= 0)
                {
                    return poseIndex;
                }

                if (_poseNameAliasesByCanonical.TryGetValue(poseName, out List<string> aliases) && aliases != null)
                {
                    int aliasIndex = FindFirstKeywordIndex(text, aliases.ToArray(), StringComparison.OrdinalIgnoreCase);
                    if (aliasIndex >= 0)
                    {
                        return aliasIndex;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(poseCategory))
            {
                string[] aliases = ResolvePoseCategoryAliases(poseCategory);
                int aliasIndex = FindFirstKeywordIndex(text, aliases, StringComparison.Ordinal);
                if (aliasIndex >= 0)
                {
                    return aliasIndex;
                }
            }

            return -1;
        }

        private IEnumerable<string> EnumeratePoseMatchNames(PoseCategoryEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.NameAnimation))
            {
                yield break;
            }

            yield return entry.NameAnimation;

            if (_poseNameAliasesByCanonical.TryGetValue(entry.NameAnimation, out List<string> aliases) && aliases != null)
            {
                foreach (string alias in aliases)
                {
                    if (string.IsNullOrWhiteSpace(alias))
                    {
                        continue;
                    }

                    yield return alias;
                }
            }
        }


        private bool TryResolveInferredPoseCategory(string text, out string category, out string matchedRuleId)
        {
            category = null;
            matchedRuleId = null;
            if (string.IsNullOrWhiteSpace(text) || _poseCategoryInferRules == null || _poseCategoryInferRules.Count <= 0)
            {
                return false;
            }
            if (!_poseInferRulesEnabled)
            {
                return false;
            }

            PoseCategoryInferRule bestRule = null;
            int bestPriority = int.MinValue;
            int bestSpecificity = int.MinValue;

            foreach (PoseCategoryInferRule rule in _poseCategoryInferRules)
            {
                if (rule == null || string.IsNullOrWhiteSpace(rule.TargetCategory))
                {
                    continue;
                }

                if (rule.Enabled == false)
                {
                    continue;
                }

                if (!IsPoseCategoryEnabled(rule.TargetCategory))
                {
                    continue;
                }

                if (!_poseEntriesByCategory.ContainsKey(rule.TargetCategory))
                {
                    continue;
                }

                if (!IsInferRuleMatch(text, rule))
                {
                    continue;
                }

                int specificity =
                    (rule.RequiredAll?.Length ?? 0) * 100 +
                    (rule.RequiredAny?.Length ?? 0) * 10 +
                    (rule.ExcludeAny?.Length ?? 0);

                bool better = rule.Priority > bestPriority ||
                              (rule.Priority == bestPriority && specificity > bestSpecificity);
                if (!better)
                {
                    continue;
                }

                bestRule = rule;
                bestPriority = rule.Priority;
                bestSpecificity = specificity;
            }

            if (bestRule == null)
            {
                return false;
            }

            category = bestRule.TargetCategory;
            matchedRuleId = bestRule.RuleId;
            return true;
        }

        private bool IsInferRuleMatch(string text, PoseCategoryInferRule rule)
        {
            if (rule == null || string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string[] excludeAny = rule.ExcludeAny ?? new string[0];
            foreach (string kw in excludeAny)
            {
                if (ContainsKeyword(text, kw))
                {
                    return false;
                }
            }

            string[] requiredAll = rule.RequiredAll ?? new string[0];
            foreach (string kw in requiredAll)
            {
                if (!ContainsKeyword(text, kw))
                {
                    return false;
                }
            }

            string[] requiredAny = rule.RequiredAny ?? new string[0];
            if (requiredAny.Length <= 0)
            {
                return true;
            }

            foreach (string kw in requiredAny)
            {
                if (ContainsKeyword(text, kw))
                {
                    return true;
                }
            }

            return false;
        }

        private PoseCategoryEntry TryPickPreferredPoseEntry(
            string category,
            string text,
            List<PoseCategoryEntry> entries)
        {
            if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(text) || entries == null || entries.Count <= 0)
            {
                return null;
            }
            if (!_poseRulesEnabled)
            {
                return null;
            }

            PoseCategoryEntry scoredPreferred = TryPickPoseEntryByTokenScore(category, text, entries);
            if (scoredPreferred != null)
            {
                return scoredPreferred;
            }

            if (string.Equals(category, "正常位系", StringComparison.Ordinal) &&
                ContainsAny(text, NormalMissionaryInterlockKeywords))
            {
                PoseCategoryEntry normalPreferred = PickRandomPoseEntryByNames(entries, NormalMissionaryInterlockPoseNames);
                if (normalPreferred != null)
                {
                    Log($"[pose] preferred matched category={category} reason=密着/しがみ pose={normalPreferred.NameAnimation}");
                    return normalPreferred;
                }
            }

            if (string.Equals(category, "立位系", StringComparison.Ordinal) &&
                text.IndexOf("壁", StringComparison.Ordinal) >= 0)
            {
                PoseCategoryEntry standingPreferred = PickRandomPoseEntryByNames(entries, StandingWallPreferredPoseNames);
                if (standingPreferred != null)
                {
                    Log($"[pose] preferred matched category={category} reason=壁 pose={standingPreferred.NameAnimation}");
                    return standingPreferred;
                }
            }

            return null;
        }

        private PoseCategoryEntry TryPickPoseEntryByTokenScore(string category, string text, List<PoseCategoryEntry> entries)
        {
            if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(text) || entries == null || entries.Count <= 0)
            {
                return null;
            }

            if (!TryFindBestScoreMatchForCategory(category, text, entries, out PoseScoreMatch bestMatch))
            {
                return null;
            }

            PoseCategoryEntry selected;
            lock (_random)
            {
                selected = bestMatch.Candidates[_random.Next(bestMatch.Candidates.Count)];
            }

            string level = bestMatch.Score >= _poseForceThreshold ? "force" : "prefer";
            Log($"[pose] scored-{level} matched category={category} rule={bestMatch.Rule.RuleId} score={bestMatch.Score} longest={bestMatch.LongestMatch} pose={selected?.NameAnimation}");
            return selected;
        }

        private bool TryPickPoseFromGlobalTokenScore(string text, out string category, out PoseCategoryEntry selected, out PoseScoreMatch match)
        {
            category = null;
            selected = null;
            match = null;

            if (string.IsNullOrWhiteSpace(text) || _poseKeywordScoreRules == null || _poseKeywordScoreRules.Count <= 0)
            {
                return false;
            }

            var candidateCategories = _poseKeywordScoreRules
                .Where(r => r != null && !string.IsNullOrWhiteSpace(r.Category))
                .Select(r => r.Category)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (candidateCategories.Length <= 0)
            {
                return false;
            }

            PoseScoreMatch bestMatch = null;
            foreach (string candidateCategory in candidateCategories)
            {
                if (!_poseEntriesByCategory.TryGetValue(candidateCategory, out var entries) || entries == null || entries.Count <= 0)
                {
                    continue;
                }

                if (!TryFindBestScoreMatchForCategory(candidateCategory, text, entries, out PoseScoreMatch current))
                {
                    continue;
                }

                if (IsBetterScoreMatch(current, bestMatch))
                {
                    bestMatch = current;
                }
            }

            if (bestMatch == null || bestMatch.Candidates == null || bestMatch.Candidates.Count <= 0)
            {
                return false;
            }

            lock (_random)
            {
                selected = bestMatch.Candidates[_random.Next(bestMatch.Candidates.Count)];
            }

            category = bestMatch.Category;
            match = bestMatch;
            return selected != null;
        }

        private bool TryFindBestScoreMatchForCategory(string category, string text, List<PoseCategoryEntry> entries, out PoseScoreMatch bestMatch)
        {
            bestMatch = null;
            if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(text) || entries == null || entries.Count <= 0)
            {
                return false;
            }
            if (!_poseRulesEnabled)
            {
                return false;
            }

            if (!IsPoseCategoryEnabled(category))
            {
                return false;
            }

            if (_poseKeywordScoreRules == null || _poseKeywordScoreRules.Count <= 0)
            {
                return false;
            }

            foreach (var rule in _poseKeywordScoreRules)
            {
                if (rule == null || rule.Tokens == null || rule.Tokens.Length <= 0 || rule.PoseNames == null || rule.PoseNames.Length <= 0)
                {
                    continue;
                }

                if (rule.Enabled == false)
                {
                    continue;
                }

                if (!IsScoreRuleCategoryMatch(category, rule.Category))
                {
                    continue;
                }

                var candidates = FindPoseEntriesByNames(entries, rule.PoseNames);
                if (candidates.Count <= 0)
                {
                    continue;
                }

                int score = _poseScoreBase;
                int matchedTokenCount = 0;
                int longestMatch = 0;

                foreach (var token in rule.Tokens)
                {
                    if (token == null || string.IsNullOrWhiteSpace(token.Keyword))
                    {
                        continue;
                    }

                    if (!ContainsKeyword(text, token.Keyword))
                    {
                        continue;
                    }

                    matchedTokenCount++;
                    score += token.Score;
                    if (token.Keyword.Length > longestMatch)
                    {
                        longestMatch = token.Keyword.Length;
                    }
                }

                if (matchedTokenCount <= 0 || score < _poseAdoptThreshold)
                {
                    continue;
                }

                var currentMatch = new PoseScoreMatch
                {
                    Category = category,
                    Rule = rule,
                    Candidates = candidates,
                    Score = score,
                    LongestMatch = longestMatch,
                    Priority = rule.Priority,
                    MatchedTokenCount = matchedTokenCount
                };

                if (IsBetterScoreMatch(currentMatch, bestMatch))
                {
                    bestMatch = currentMatch;
                }
            }

            return bestMatch != null;
        }

        private static bool IsBetterScoreMatch(PoseScoreMatch current, PoseScoreMatch best)
        {
            if (current == null)
            {
                return false;
            }

            if (best == null)
            {
                return true;
            }

            return current.Score > best.Score ||
                   (current.Score == best.Score && current.LongestMatch > best.LongestMatch) ||
                   (current.Score == best.Score && current.LongestMatch == best.LongestMatch && current.Priority > best.Priority) ||
                   (current.Score == best.Score && current.LongestMatch == best.LongestMatch && current.Priority == best.Priority && current.MatchedTokenCount > best.MatchedTokenCount);
        }

        private static bool IsScoreRuleCategoryMatch(string selectedCategory, string ruleCategory)
        {
            if (string.IsNullOrWhiteSpace(selectedCategory) || string.IsNullOrWhiteSpace(ruleCategory))
            {
                return false;
            }

            if (string.Equals(selectedCategory, ruleCategory, StringComparison.Ordinal))
            {
                return true;
            }


            return false;
        }

        private bool IsPoseCategoryEnabled(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return false;
            }

            if (_poseCategoryEnabled.TryGetValue(category, out bool enabled))
            {
                return enabled;
            }

            return true;
        }

        private static bool ContainsKeyword(string text, string keyword)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(keyword))
            {
                return false;
            }

            bool asciiOnly = keyword.All(c => c <= sbyte.MaxValue);
            StringComparison comparison = asciiOnly ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return text.IndexOf(keyword, comparison) >= 0;
        }

        private PoseCategoryEntry PickRandomPoseEntryByNames(List<PoseCategoryEntry> entries, string[] preferredNames)
        {
            if (entries == null || entries.Count <= 0 || preferredNames == null || preferredNames.Length <= 0)
            {
                return null;
            }

            var matched = FindPoseEntriesByNames(entries, preferredNames);

            if (matched.Count <= 0)
            {
                return null;
            }

            lock (_random)
            {
                return matched[_random.Next(matched.Count)];
            }
        }

        private static List<PoseCategoryEntry> FindPoseEntriesByNames(List<PoseCategoryEntry> entries, string[] preferredNames)
        {
            if (entries == null || entries.Count <= 0 || preferredNames == null || preferredNames.Length <= 0)
            {
                return new List<PoseCategoryEntry>();
            }

            var exact = entries
                .Where(e => e != null && !string.IsNullOrWhiteSpace(e.NameAnimation))
                .Where(e => preferredNames.Any(name =>
                    !string.IsNullOrWhiteSpace(name) &&
                    string.Equals(e.NameAnimation, name, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (exact.Count > 0)
            {
                return exact;
            }

            return entries
                .Where(e => e != null && !string.IsNullOrWhiteSpace(e.NameAnimation))
                .Where(e => preferredNames.Any(name =>
                    !string.IsNullOrWhiteSpace(name) &&
                    e.NameAnimation.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();
        }

        private static string BuildPoseRuleEntryLabel(PoseKeywordScoreRule rule, int index)
        {
            string category = rule != null && !string.IsNullOrWhiteSpace(rule.Category) ? rule.Category : "未分類";
            string[] tokens = (rule?.Tokens ?? new PoseKeywordScoreToken[0])
                .Where(t => t != null && !string.IsNullOrWhiteSpace(t.Keyword))
                .Select(t => t.Keyword)
                .Distinct(StringComparer.Ordinal)
                .Take(2)
                .ToArray();
            string hint = tokens.Length > 0
                ? string.Join("+", tokens)
                : (rule?.RuleId ?? "rule");
            hint = TruncateForConfigLabel(hint, 16);
            return $"{index:D2} {category}:{hint}";
        }

        private static string BuildPoseInferRuleEntryLabel(PoseCategoryInferRule rule, int index)
        {
            string category = rule != null && !string.IsNullOrWhiteSpace(rule.TargetCategory) ? rule.TargetCategory : "未分類";
            string hint = ExtractInferRuleHint(rule);
            hint = TruncateForConfigLabel(hint, 18);
            return $"{index:D2} {category}:{hint}";
        }

        private static string ExtractInferRuleHint(PoseCategoryInferRule rule)
        {
            string[] reqAll = rule?.RequiredAll ?? new string[0];
            string[] reqAny = rule?.RequiredAny ?? new string[0];
            if (reqAll.Length > 0 && reqAny.Length > 0)
            {
                return reqAll[0] + "+" + reqAny[0];
            }
            if (reqAll.Length > 0)
            {
                return reqAll[0];
            }
            if (reqAny.Length > 0)
            {
                return reqAny[0];
            }
            return rule?.RuleId ?? "infer";
        }

        private static string TruncateForConfigLabel(string text, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            if (maxLength <= 0 || text.Length <= maxLength)
            {
                return text;
            }

            return text.Substring(0, maxLength) + "…";
        }

        private string[] ResolvePoseCategoryAliases(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return new string[0];
            }

            var aliases = new List<string>();
            if (_categoryAliases.TryGetValue(category, out var mapped) && mapped != null)
            {
                aliases.AddRange(mapped.Where(x => !string.IsNullOrWhiteSpace(x)));
            }

            aliases.Add(category);
            if (category.EndsWith("系", StringComparison.Ordinal) && category.Length > 1)
            {
                aliases.Add(category.Substring(0, category.Length - 1));
            }

            return aliases
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .OrderByDescending(x => x.Length)
                .ToArray();
        }

        private bool TrySelectVideoFileNameFromText(string text, out string selectedFileName, out int httpPort, out string reason)
        {
            selectedFileName = null;
            reason = "unknown";
            PluginSettings settings = Settings;
            httpPort = settings != null ? settings.BlankMapAddHttpPort : DefaultBlankMapAddHttpPort;

            if (settings == null)
            {
                reason = "settings unavailable";
                return false;
            }

            if (!settings.EnableVideoPlaybackByResponseText)
            {
                reason = "video response-text playback disabled";
                return false;
            }

            string[] triggerKeywords = SplitKeywords(settings.VideoPlaybackTriggerKeywords);
            if (triggerKeywords.Length <= 0)
            {
                triggerKeywords = new[] { "流す" };
            }

            if (!ContainsAny(text, triggerKeywords))
            {
                reason = "trigger keyword not found";
                return false;
            }

            if (!TryLoadBlankMapAddFolderInfo(settings, out string folderPath, out int resolvedPort, out string loadReason))
            {
                httpPort = resolvedPort;
                reason = loadReason;
                return false;
            }

            httpPort = resolvedPort;

            HashSet<string> allowedExt = BuildVideoExtensionSet(settings.VideoFileExtensions);
            string[] allFiles;
            try
            {
                allFiles = Directory.GetFiles(folderPath);
            }
            catch (Exception ex)
            {
                reason = "folder scan failed: " + ex.Message;
                return false;
            }

            List<VideoTrackGroup> trackGroups = BuildVideoTrackGroups(allFiles, allowedExt);
            if (trackGroups.Count <= 0)
            {
                reason = "no video track groups";
                return false;
            }

            if (!TryFindVideoTrackGroupAfterTrigger(
                text,
                triggerKeywords,
                trackGroups,
                out VideoTrackGroup matchedGroup,
                out int lineNumber,
                out string matchedTrigger,
                out string matchedLineTail))
            {
                reason = "no track group after trigger line";
                return false;
            }

            List<string> candidates = matchedGroup.FileNames;
            if (candidates == null || candidates.Count <= 0)
            {
                reason = "matched track group has no files: " + matchedGroup.CanonicalName;
                return false;
            }

            lock (_random)
            {
                selectedFileName = candidates[_random.Next(candidates.Count)];
            }

            reason = "matched group='" + matchedGroup.CanonicalName + "' variants=" + candidates.Count
                + " line=" + lineNumber + " trigger='" + matchedTrigger + "'";
            Log("[video] track group matched canonical='" + matchedGroup.CanonicalName
                + "' variants=" + candidates.Count
                + " selected='" + selectedFileName + "'"
                + " line=" + lineNumber
                + " trigger='" + matchedTrigger + "'"
                + " tail='" + TrimPreview(matchedLineTail, 80) + "'");
            return true;
        }

        private static List<VideoTrackGroup> BuildVideoTrackGroups(string[] allFiles, HashSet<string> allowedExt)
        {
            var groups = new Dictionary<string, VideoTrackGroup>(StringComparer.Ordinal);
            if (allFiles == null || allFiles.Length <= 0)
            {
                return new List<VideoTrackGroup>();
            }

            for (int i = 0; i < allFiles.Length; i++)
            {
                string path = allFiles[i];
                string ext = Path.GetExtension(path) ?? string.Empty;
                if (allowedExt != null && allowedExt.Count > 0 && !allowedExt.Contains(ext))
                {
                    continue;
                }

                string fileName = Path.GetFileName(path);
                string baseName = Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(baseName))
                {
                    continue;
                }

                string canonicalName = StripVideoVariantSuffix(baseName);
                string normalizedName = NormalizeVideoLookupText(canonicalName);
                if (string.IsNullOrWhiteSpace(normalizedName))
                {
                    continue;
                }

                if (!groups.TryGetValue(normalizedName, out VideoTrackGroup group))
                {
                    group = new VideoTrackGroup
                    {
                        CanonicalName = canonicalName,
                        NormalizedName = normalizedName
                    };
                    groups[normalizedName] = group;
                }

                if (!group.FileNames.Any(x => string.Equals(x, fileName, StringComparison.OrdinalIgnoreCase)))
                {
                    group.FileNames.Add(fileName);
                }
            }

            foreach (VideoTrackGroup group in groups.Values)
            {
                group.FileNames.Sort(StringComparer.OrdinalIgnoreCase);
            }

            return groups.Values
                .OrderByDescending(x => x.NormalizedName.Length)
                .ThenBy(x => x.CanonicalName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool TryFindVideoTrackGroupAfterTrigger(
            string text,
            string[] triggerKeywords,
            List<VideoTrackGroup> trackGroups,
            out VideoTrackGroup matchedGroup,
            out int lineNumber,
            out string matchedTrigger,
            out string matchedLineTail)
        {
            matchedGroup = null;
            lineNumber = -1;
            matchedTrigger = string.Empty;
            matchedLineTail = string.Empty;

            if (string.IsNullOrWhiteSpace(text) || triggerKeywords == null || triggerKeywords.Length <= 0
                || trackGroups == null || trackGroups.Count <= 0)
            {
                return false;
            }

            string[] lines = SplitVideoResponseLines(text);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i] ?? string.Empty;
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                List<VideoTriggerHit> hits = FindVideoTriggerHits(line, triggerKeywords);
                if (hits.Count <= 0)
                {
                    continue;
                }

                for (int h = 0; h < hits.Count; h++)
                {
                    VideoTriggerHit hit = hits[h];
                    if (hit == null || string.IsNullOrWhiteSpace(hit.Keyword))
                    {
                        continue;
                    }

                    int tailStart = hit.Index + hit.Keyword.Length;
                    if (tailStart < 0 || tailStart > line.Length)
                    {
                        continue;
                    }

                    string tail = line.Substring(tailStart);
                    string normalizedTail = NormalizeVideoLookupText(tail);
                    if (string.IsNullOrWhiteSpace(normalizedTail))
                    {
                        continue;
                    }

                    VideoTrackGroup group = FindBestVideoTrackGroupInTail(normalizedTail, trackGroups);
                    if (group == null)
                    {
                        continue;
                    }

                    matchedGroup = group;
                    lineNumber = i + 1;
                    matchedTrigger = hit.Keyword;
                    matchedLineTail = tail.Trim();
                    return true;
                }
            }

            return false;
        }

        private static string[] SplitVideoResponseLines(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new string[0];
            }

            return text
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split(new[] { '\n' }, StringSplitOptions.None);
        }

        private static List<VideoTriggerHit> FindVideoTriggerHits(string line, string[] triggerKeywords)
        {
            var hits = new List<VideoTriggerHit>();
            if (string.IsNullOrWhiteSpace(line) || triggerKeywords == null)
            {
                return hits;
            }

            for (int i = 0; i < triggerKeywords.Length; i++)
            {
                string keyword = triggerKeywords[i];
                if (string.IsNullOrWhiteSpace(keyword))
                {
                    continue;
                }

                int start = 0;
                while (start < line.Length)
                {
                    int index = line.IndexOf(keyword, start, StringComparison.Ordinal);
                    if (index < 0)
                    {
                        break;
                    }

                    hits.Add(new VideoTriggerHit
                    {
                        Index = index,
                        Keyword = keyword
                    });

                    start = index + Math.Max(1, keyword.Length);
                }
            }

            return hits
                .OrderBy(x => x.Index)
                .ThenByDescending(x => x.Keyword == null ? 0 : x.Keyword.Length)
                .ToList();
        }

        private static VideoTrackGroup FindBestVideoTrackGroupInTail(string normalizedTail, List<VideoTrackGroup> trackGroups)
        {
            if (string.IsNullOrWhiteSpace(normalizedTail) || trackGroups == null || trackGroups.Count <= 0)
            {
                return null;
            }

            VideoTrackGroup bestGroup = null;
            int bestLength = -1;
            int bestIndex = int.MaxValue;
            for (int i = 0; i < trackGroups.Count; i++)
            {
                VideoTrackGroup group = trackGroups[i];
                if (group == null || string.IsNullOrWhiteSpace(group.NormalizedName))
                {
                    continue;
                }

                int index = normalizedTail.IndexOf(group.NormalizedName, StringComparison.Ordinal);
                if (index < 0)
                {
                    continue;
                }

                int length = group.NormalizedName.Length;
                if (length > bestLength || (length == bestLength && index < bestIndex))
                {
                    bestGroup = group;
                    bestLength = length;
                    bestIndex = index;
                }
            }

            return bestGroup;
        }

        private static string StripVideoVariantSuffix(string baseName)
        {
            string name = (baseName ?? string.Empty).Trim();
            if (name.Length <= 0)
            {
                return string.Empty;
            }

            int close = name.Length - 1;
            while (close >= 0 && char.IsWhiteSpace(name[close]))
            {
                close--;
            }

            if (close <= 0)
            {
                return name;
            }

            char closeChar = name[close];
            char openChar;
            if (closeChar == ')')
            {
                openChar = '(';
            }
            else if (closeChar == '）')
            {
                openChar = '（';
            }
            else
            {
                return name;
            }

            int open = name.LastIndexOf(openChar, close);
            if (open < 0)
            {
                return name;
            }

            string inner = name.Substring(open + 1, close - open - 1).Trim();
            if (inner.Length <= 0)
            {
                return name;
            }

            for (int i = 0; i < inner.Length; i++)
            {
                if (!char.IsDigit(inner[i]))
                {
                    return name;
                }
            }

            return name.Substring(0, open).TrimEnd();
        }

        private static string NormalizeVideoLookupText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string normalized = text.Normalize(NormalizationForm.FormKC);
            var sb = new StringBuilder(normalized.Length);
            for (int i = 0; i < normalized.Length; i++)
            {
                char c = normalized[i];
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(char.ToLowerInvariant(c));
                }
            }

            return sb.ToString();
        }

        private bool TryLoadBlankMapAddFolderInfo(PluginSettings settings, out string folderPath, out int httpPort, out string reason)
        {
            folderPath = string.Empty;
            reason = "unknown";
            httpPort = settings != null ? settings.BlankMapAddHttpPort : DefaultBlankMapAddHttpPort;

            if (settings == null)
            {
                reason = "settings unavailable";
                return false;
            }

            string configuredPath = string.IsNullOrWhiteSpace(settings.BlankMapAddSettingsRelativePath)
                ? "..\\MainGameBlankMapAdd\\MapAddSettings.json"
                : settings.BlankMapAddSettingsRelativePath.Trim();

            string settingsPath;
            try
            {
                settingsPath = Path.IsPathRooted(configuredPath)
                    ? Path.GetFullPath(configuredPath)
                    : Path.GetFullPath(Path.Combine(PluginDir ?? string.Empty, configuredPath));
            }
            catch (Exception ex)
            {
                reason = "invalid blank map settings path: " + ex.Message;
                return false;
            }

            if (!File.Exists(settingsPath))
            {
                reason = "blank map settings not found: " + settingsPath;
                return false;
            }

            BlankMapAddSettingsSnapshot snapshot;
            try
            {
                string json = File.ReadAllText(settingsPath, Encoding.UTF8);
                var serializer = new DataContractJsonSerializer(typeof(BlankMapAddSettingsSnapshot));
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                using (var ms = new MemoryStream(bytes))
                {
                    snapshot = serializer.ReadObject(ms) as BlankMapAddSettingsSnapshot;
                }
            }
            catch (Exception ex)
            {
                reason = "blank map settings parse failed: " + ex.Message;
                return false;
            }

            if (snapshot == null)
            {
                reason = "blank map settings parse returned null";
                return false;
            }

            if (snapshot.HttpEnabled.HasValue && !snapshot.HttpEnabled.Value)
            {
                reason = "blank map http disabled";
                return false;
            }

            if (snapshot.HttpPort.HasValue && snapshot.HttpPort.Value > 0 && snapshot.HttpPort.Value <= 65535)
            {
                httpPort = snapshot.HttpPort.Value;
            }

            if (httpPort <= 0 || httpPort > 65535)
            {
                httpPort = DefaultBlankMapAddHttpPort;
            }

            string rawFolder = (snapshot.FolderPlayPath ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(rawFolder))
            {
                reason = "FolderPlayPath is empty";
                return false;
            }

            try
            {
                folderPath = Path.IsPathRooted(rawFolder)
                    ? Path.GetFullPath(rawFolder)
                    : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(settingsPath) ?? string.Empty, rawFolder));
            }
            catch (Exception ex)
            {
                reason = "folder path invalid: " + ex.Message;
                return false;
            }

            if (!Directory.Exists(folderPath))
            {
                reason = "folder not found: " + folderPath;
                return false;
            }

            reason = "ok";
            return true;
        }

        private void PostVideoPlayByFileName(string fileName, int httpPort)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                LogWarn("[video] filename is empty");
                return;
            }

            PluginSettings settings = Settings;
            string endpointPath = settings != null ? settings.VideoPlayEndpointPath : "/videoroom/play";
            if (string.IsNullOrWhiteSpace(endpointPath))
            {
                endpointPath = "/videoroom/play";
            }

            endpointPath = endpointPath.Trim();
            if (!endpointPath.StartsWith("/", StringComparison.Ordinal))
            {
                endpointPath = "/" + endpointPath;
            }

            int port = httpPort;
            if (port <= 0 || port > 65535)
            {
                port = settings != null ? settings.BlankMapAddHttpPort : DefaultBlankMapAddHttpPort;
            }
            if (port <= 0 || port > 65535)
            {
                port = DefaultBlankMapAddHttpPort;
            }

            string url = "http://127.0.0.1:" + port + endpointPath;
            string payload = "{\"filename\":\"" + EscapeJsonValue(fileName) + "\"}";
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.ContentType = "application/json; charset=utf-8";
                request.Timeout = 1500;
                request.ReadWriteTimeout = 1500;

                byte[] body = Encoding.UTF8.GetBytes(payload);
                request.ContentLength = body.Length;
                using (Stream stream = request.GetRequestStream())
                {
                    stream.Write(body, 0, body.Length);
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    string responseText = reader.ReadToEnd();
                    Log($"[video] play sent filename='{fileName}' status={(int)response.StatusCode} port={port}");
                    if (!string.IsNullOrWhiteSpace(responseText))
                    {
                        Log("[video] play response: " + TrimPreview(responseText, 120));
                    }
                }
            }
            catch (WebException webEx)
            {
                string detail = webEx.Message;
                try
                {
                    if (webEx.Response != null)
                    {
                        using (var stream = webEx.Response.GetResponseStream())
                        using (var reader = new StreamReader(stream ?? Stream.Null, Encoding.UTF8))
                        {
                            string body = reader.ReadToEnd();
                            if (!string.IsNullOrWhiteSpace(body))
                            {
                                detail += " body=" + TrimPreview(body, 120);
                            }
                        }
                    }
                }
                catch { }
                LogWarn("[video] play request failed: " + detail + " url=" + url);
            }
            catch (Exception ex)
            {
                LogWarn("[video] play request error: " + ex.Message + " url=" + url);
            }
        }

        private static string[] SplitFileNameIntoWords(string baseName)
        {
            if (string.IsNullOrWhiteSpace(baseName))
                return new string[0];

            char[] splitChars = { ' ', '　', '-', '_', '(', ')', '（', '）', '[', ']', '【', '】',
                '.', '·', '·', '/', '\\', '&', '+', '=', '#', '@', '~', '～' };
            return baseName.Split(splitChars, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length >= 2)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string[] BuildVideoMatchTokens(string text, string[] triggerKeywords)
        {
            var tokens = new List<string>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return tokens.ToArray();
            }

            string[] keywords = triggerKeywords ?? new string[0];
            for (int i = 0; i < keywords.Length; i++)
            {
                string keyword = keywords[i];
                if (string.IsNullOrWhiteSpace(keyword))
                {
                    continue;
                }

                int start = 0;
                while (true)
                {
                    int idx = text.IndexOf(keyword, start, StringComparison.Ordinal);
                    if (idx < 0)
                    {
                        break;
                    }

                    int beforeStart = Math.Max(0, idx - 32);
                    string before = text.Substring(beforeStart, idx - beforeStart);
                    string beforeToken = ExtractTailVideoToken(before);
                    if (!string.IsNullOrWhiteSpace(beforeToken))
                    {
                        tokens.Add(beforeToken);
                    }

                    int afterStart = idx + keyword.Length;
                    int afterLength = Math.Min(32, Math.Max(0, text.Length - afterStart));
                    if (afterLength > 0)
                    {
                        string after = text.Substring(afterStart, afterLength);
                        string afterToken = ExtractHeadVideoToken(after);
                        if (!string.IsNullOrWhiteSpace(afterToken))
                        {
                            tokens.Add(afterToken);
                        }
                    }

                    start = idx + keyword.Length;
                }
            }

            return tokens
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string ExtractTailVideoToken(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string tail = text.Trim();
            char[] separators = { ' ', '　', '\t', '\r', '\n', '、', '。', '！', '!', '？', '?', ',', '，', '・', '」', '』', '"', '\'' };
            int cut = tail.LastIndexOfAny(separators);
            if (cut >= 0 && cut < tail.Length - 1)
            {
                tail = tail.Substring(cut + 1);
            }

            tail = tail.Trim();
            if (string.IsNullOrWhiteSpace(tail))
            {
                return string.Empty;
            }

            string[] suffixes =
            {
                "を", "で", "に", "へ", "は", "が", "と", "ね", "よ",
                "動画", "ビデオ", "再生", "して", "する"
            };
            bool changed = true;
            while (changed && !string.IsNullOrWhiteSpace(tail))
            {
                changed = false;
                for (int i = 0; i < suffixes.Length; i++)
                {
                    string suffix = suffixes[i];
                    if (tail.Length > suffix.Length && tail.EndsWith(suffix, StringComparison.Ordinal))
                    {
                        tail = tail.Substring(0, tail.Length - suffix.Length).Trim();
                        changed = true;
                    }
                }
            }

            return tail.Length >= 2 ? tail : string.Empty;
        }

        private static string ExtractHeadVideoToken(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string head = text.Trim();
            if (string.IsNullOrWhiteSpace(head))
            {
                return string.Empty;
            }

            string[] prefixes =
            {
                "を", "で", "に", "へ", "は", "が", "と", "ね", "よ",
                "動画", "ビデオ", "再生", "して", "する"
            };
            bool changed = true;
            while (changed && !string.IsNullOrWhiteSpace(head))
            {
                changed = false;
                for (int i = 0; i < prefixes.Length; i++)
                {
                    string prefix = prefixes[i];
                    if (head.Length > prefix.Length && head.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        head = head.Substring(prefix.Length).TrimStart();
                        changed = true;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(head))
            {
                return string.Empty;
            }

            char[] separators = { ' ', '　', '\t', '\r', '\n', '、', '。', '！', '!', '？', '?', ',', '，', '・', '」', '』', '"', '\'' };
            int cut = head.IndexOfAny(separators);
            if (cut > 0)
            {
                head = head.Substring(0, cut);
            }
            else if (cut == 0)
            {
                return string.Empty;
            }

            head = head.Trim();
            return head.Length >= 2 ? head : string.Empty;
        }

        private static HashSet<string> BuildVideoExtensionSet(string csv)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] parts = SplitKeywords(csv);
            for (int i = 0; i < parts.Length; i++)
            {
                string ext = parts[i];
                if (string.IsNullOrWhiteSpace(ext))
                {
                    continue;
                }

                ext = ext.Trim();
                if (!ext.StartsWith(".", StringComparison.Ordinal))
                {
                    ext = "." + ext;
                }
                set.Add(ext);
            }

            if (set.Count <= 0)
            {
                for (int i = 0; i < DefaultVideoExtensions.Length; i++)
                {
                    set.Add(DefaultVideoExtensions[i]);
                }
            }

            return set;
        }

        private static string TrimPreview(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            if (maxLength <= 0 || text.Length <= maxLength)
            {
                return text;
            }

            return text.Substring(0, maxLength);
        }

        private static string EscapeJsonValue(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        // ParseCoordFromText は廃止。呼び出し元 HandleResponseTextCommand に統合済み。

        private List<TimedClothesItem> ParseTimedClothesFromText(string text)
        {
            var results = new List<TimedClothesItem>();
            if (string.IsNullOrEmpty(text))
            {
                return results;
            }

            string[] removeAllKeywords = SplitKeywords(_cfgRemoveAllKeywords?.Value ?? "全裸になるね,全部脱ぐね,全部脱いじゃう");
            int removeAllIndex = FindFirstKeywordIndex(text, removeAllKeywords, StringComparison.Ordinal);
            if (removeAllIndex >= 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    results.Add(new TimedClothesItem
                    {
                        Item = new ClothesItem { kind = i, state = 3 },
                        MatchIndex = removeAllIndex,
                        PartKeyword = "all",
                        ActionKeyword = "remove_all"
                    });
                }

                return results
                    .OrderBy(x => x.MatchIndex)
                    .ThenBy(x => x.Item.kind)
                    .ToList();
            }

            string[] putOnAllKeywords = SplitKeywords(_cfgPutOnAllKeywords?.Value ?? "全部着るね");
            int putOnAllIndex = FindFirstKeywordIndex(text, putOnAllKeywords, StringComparison.Ordinal);
            if (putOnAllIndex >= 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    results.Add(new TimedClothesItem
                    {
                        Item = new ClothesItem { kind = i, state = 0 },
                        MatchIndex = putOnAllIndex,
                        PartKeyword = "all",
                        ActionKeyword = "put_on_all"
                    });
                }

                return results
                    .OrderBy(x => x.MatchIndex)
                    .ThenBy(x => x.Item.kind)
                    .ToList();
            }

            string[][] partKeywords =
            {
                SplitKeywords(_cfgTopKeywords?.Value ?? "上着,ジャケット,トップス"),
                SplitKeywords(_cfgBottomKeywords?.Value ?? "スカート,ホットパンツ,ミニスカ,ボトムス,ズボン,パンツ"),
                SplitKeywords(_cfgBraKeywords?.Value ?? "ブラ"),
                SplitKeywords(_cfgShortsKeywords?.Value ?? "パンティー,パンティ"),
                SplitKeywords(_cfgGlovesKeywords?.Value ?? "グローブ,手袋"),
                SplitKeywords(_cfgPanthoseKeywords?.Value ?? "ガーターベルト,パンスト,ガーター"),
                SplitKeywords(_cfgSocksKeywords?.Value ?? "ストッキング,ニーハイ,靴下"),
                SplitKeywords(_cfgShoesKeywords?.Value ?? "ハイヒール,スニーカー,サンダル,ヒール,靴"),
            };

            string[] removeKeywords = SplitKeywords(_cfgRemoveKeywords?.Value ?? "脱ぐね,脱いじゃう");
            string[] shiftKeywords = SplitKeywords(_cfgShiftKeywords?.Value ?? "ずらすね,半脱ぎにするね");
            string[] putOnKeywords = SplitKeywords(_cfgPutOnKeywords?.Value ?? "着るね,付けるね");

            var actionGroups = new[]
            {
                Tuple.Create(removeKeywords, 3),
                Tuple.Create(shiftKeywords, -1),
                Tuple.Create(putOnKeywords, 0),
            };

            foreach (TextLineSpan lineSpan in SplitTextLinesWithOffsets(text))
            {
                string line = lineSpan?.Line ?? string.Empty;
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                foreach (Tuple<string[], int> actionGroup in actionGroups)
                {
                    string[] actionKeywords = actionGroup.Item1;
                    int state = actionGroup.Item2;
                    foreach (string actionKeyword in actionKeywords)
                    {
                        if (string.IsNullOrWhiteSpace(actionKeyword))
                        {
                            continue;
                        }

                        int searchIndex = 0;
                        while (searchIndex < line.Length)
                        {
                            int actionPos = line.IndexOf(actionKeyword, searchIndex, StringComparison.Ordinal);
                            if (actionPos < 0)
                            {
                                break;
                            }

                            searchIndex = actionPos + Math.Max(1, actionKeyword.Length);
                            int actionAbs = lineSpan.StartIndex + actionPos;

                            for (int kind = 0; kind < partKeywords.Length; kind++)
                            {
                                if (!TryFindNearestKeywordBeforeIndex(line, partKeywords[kind], actionPos, out string partKeyword, out _))
                                {
                                    continue;
                                }

                                results.Add(new TimedClothesItem
                                {
                                    Item = new ClothesItem { kind = kind, state = state },
                                    MatchIndex = actionAbs,
                                    PartKeyword = partKeyword,
                                    ActionKeyword = actionKeyword
                                });
                            }
                        }
                    }
                }
            }

            return results
                .OrderBy(x => x.MatchIndex)
                .ThenBy(x => x.Item.kind)
                .ToList();
        }

        private bool TryParseTimedGlassesStateFromText(
            string text,
            out int glassesState,
            out int matchIndex,
            out string partKeyword,
            out string actionKeyword)
        {
            glassesState = -1;
            matchIndex = -1;
            partKeyword = null;
            actionKeyword = null;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            string[] glassesKeywords = SplitKeywords(_cfgGlassesKeywords?.Value ?? "メガネ,眼鏡,めがね");
            if (glassesKeywords.Length <= 0)
            {
                return false;
            }

            string[] removeKeywords = SplitKeywords(_cfgRemoveKeywords?.Value ?? "脱ぐね,脱いじゃう");
            string[] putOnKeywords = SplitKeywords(_cfgPutOnKeywords?.Value ?? "着るね,付けるね");
            var actionGroups = new[]
            {
                Tuple.Create(removeKeywords, 0),
                Tuple.Create(putOnKeywords, 1)
            };

            int bestIndex = int.MaxValue;
            foreach (TextLineSpan lineSpan in SplitTextLinesWithOffsets(text))
            {
                string line = lineSpan?.Line ?? string.Empty;
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                foreach (Tuple<string[], int> actionGroup in actionGroups)
                {
                    string[] actionKeywords = actionGroup.Item1;
                    int state = actionGroup.Item2;
                    foreach (string keyword in actionKeywords)
                    {
                        if (string.IsNullOrWhiteSpace(keyword))
                        {
                            continue;
                        }

                        int searchIndex = 0;
                        while (searchIndex < line.Length)
                        {
                            int actionPos = line.IndexOf(keyword, searchIndex, StringComparison.Ordinal);
                            if (actionPos < 0)
                            {
                                break;
                            }

                            searchIndex = actionPos + Math.Max(1, keyword.Length);
                            if (!TryFindNearestKeywordBeforeIndex(line, glassesKeywords, actionPos, out string foundPartKeyword, out _))
                            {
                                continue;
                            }

                            int actionAbs = lineSpan.StartIndex + actionPos;
                            if (actionAbs >= bestIndex)
                            {
                                continue;
                            }

                            bestIndex = actionAbs;
                            glassesState = state;
                            matchIndex = actionAbs;
                            partKeyword = foundPartKeyword;
                            actionKeyword = keyword;
                        }
                    }
                }
            }

            return glassesState >= 0;
        }

        private static bool TryFindNearestKeywordBeforeIndex(
            string line,
            string[] keywords,
            int beforeIndex,
            out string foundKeyword,
            out int foundPos)
        {
            foundKeyword = null;
            foundPos = -1;
            if (string.IsNullOrEmpty(line) || keywords == null || keywords.Length <= 0 || beforeIndex <= 0)
            {
                return false;
            }

            foreach (string keyword in keywords)
            {
                if (string.IsNullOrWhiteSpace(keyword))
                {
                    continue;
                }

                int pos = FindLastIndexBefore(line, keyword, beforeIndex, StringComparison.Ordinal);
                if (pos < 0)
                {
                    continue;
                }

                if (pos + keyword.Length > beforeIndex)
                {
                    continue;
                }

                if (pos > foundPos || (pos == foundPos && (foundKeyword == null || keyword.Length > foundKeyword.Length)))
                {
                    foundKeyword = keyword;
                    foundPos = pos;
                }
            }

            return foundPos >= 0;
        }

        private static string[] SplitKeywords(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return new string[0];
            string[] parts = csv.Split(',');
            var list = new List<string>();
            foreach (string p in parts)
            {
                string t = p.Trim();
                if (!string.IsNullOrEmpty(t)) list.Add(t);
            }
            return list.ToArray();
        }

        private static bool ContainsAny(string text, string[] keywords)
        {
            foreach (string kw in keywords)
                if (text.IndexOf(kw, StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        private List<CameraPresetTriggerHit> FindCameraPresetTriggerHitsFromText(string text)
        {
            var hits = new List<CameraPresetTriggerHit>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return hits;
            }

            string[] triggerKeywords = SplitKeywords((_cfgCameraTriggerKeywords != null ? _cfgCameraTriggerKeywords.Value : Settings?.CameraTriggerKeywords) ?? "カメラにして,カメラ切り替えて,視点にして,視点切り替えて,アングルにして,で見せて");
            if (triggerKeywords == null || triggerKeywords.Length <= 0)
            {
                return hits;
            }

            string[] presetNames;
            string presetReason;
            if (!TryGetCameraPresetNamesExternal(out presetNames, out presetReason) || presetNames == null || presetNames.Length <= 0)
            {
                LogWarn("[response_text] camera preset names unavailable reason=" + (string.IsNullOrWhiteSpace(presetReason) ? "unknown" : presetReason));
                return hits;
            }

            string[] lines = SplitVideoResponseLines(text);
            int globalOffset = 0;
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex] ?? string.Empty;
                if (string.IsNullOrWhiteSpace(line))
                {
                    globalOffset += line.Length + 1;
                    continue;
                }

                List<VideoTriggerHit> triggerHits = FindVideoTriggerHits(line, triggerKeywords);
                if (triggerHits == null || triggerHits.Count <= 0)
                {
                    globalOffset += line.Length + 1;
                    continue;
                }

                foreach (VideoTriggerHit triggerHit in triggerHits.OrderBy(x => x.Index))
                {
                    if (triggerHit == null)
                    {
                        continue;
                    }

                    string prefix = line.Substring(0, Mathf.Clamp(triggerHit.Index, 0, line.Length));
                    string matchedPreset = FindBestCameraPresetNameInPrefix(prefix, presetNames, out int presetIndexInPrefix);
                    if (string.IsNullOrWhiteSpace(matchedPreset))
                    {
                        continue;
                    }

                    hits.Add(new CameraPresetTriggerHit
                    {
                        LineIndex = lineIndex,
                        MatchIndex = globalOffset + Mathf.Max(0, presetIndexInPrefix),
                        TriggerKeyword = triggerHit.Keyword,
                        PresetName = matchedPreset
                    });
                }

                globalOffset += line.Length + 1;
            }

            return hits;
        }

        private static string FindBestCameraPresetNameInPrefix(string prefix, string[] presetNames, out int presetIndex)
        {
            presetIndex = -1;
            if (string.IsNullOrWhiteSpace(prefix) || presetNames == null || presetNames.Length <= 0)
            {
                return null;
            }

            string bestName = null;
            int bestIndex = -1;
            for (int i = 0; i < presetNames.Length; i++)
            {
                string presetName = presetNames[i];
                if (string.IsNullOrWhiteSpace(presetName))
                {
                    continue;
                }

                int found = prefix.LastIndexOf(presetName, StringComparison.Ordinal);
                if (found < 0)
                {
                    continue;
                }

                if (found > bestIndex
                    || (found == bestIndex && bestName != null && presetName.Length > bestName.Length)
                    || (found == bestIndex && bestName == null))
                {
                    bestIndex = found;
                    bestName = presetName;
                }
            }

            presetIndex = bestIndex;
            return bestName;
        }

        private static bool TryGetCameraPresetNamesExternal(out string[] presetNames, out string reason)
        {
            presetNames = new string[0];
            reason = string.Empty;
            try
            {
                Type apiType = Type.GetType("MainGameCameraControl.MainGameCameraControlApi, MainGameCameraControl", throwOnError: false);
                if (apiType == null)
                {
                    reason = "api_type_not_found";
                    return false;
                }

                var method = apiType.GetMethod("TryGetPresetNames", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (method == null)
                {
                    reason = "api_method_not_found";
                    return false;
                }

                object[] args = { null, null };
                object result = method.Invoke(null, args);
                presetNames = args[0] as string[] ?? new string[0];
                reason = args[1] as string ?? string.Empty;
                return result is bool && (bool)result;
            }
            catch (Exception ex)
            {
                reason = "exception:" + ex.Message;
                presetNames = new string[0];
                return false;
            }
        }

        // ----------------------------------------------------------------
        // pose コマンド: nameAnimation で体位を切り替える
        // ----------------------------------------------------------------

        private void HandlePoseCommand(ExternalVoiceFaceCommand command)
        {
            string name = (command.poseName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(name))
            {
                LogWarn("[pose] poseName is empty");
                return;
            }

            HSceneProc proc = CurrentProc;
            if (proc == null)
            {
                LogWarn("[pose] HSceneProc not available");
                return;
            }

            var lists = LstUseAnimInfoField?.GetValue(proc) as List<HSceneProc.AnimationListInfo>[];
            if (lists == null)
            {
                LogWarn("[pose] lstUseAnimInfo not found");
                return;
            }

            bool filterMode = command.poseMode >= 0 && System.Enum.IsDefined(typeof(HFlag.EMode), command.poseMode);
            HFlag.EMode targetMode = filterMode ? (HFlag.EMode)command.poseMode : HFlag.EMode.none;

            int bestScore = int.MinValue;
            HSceneProc.AnimationListInfo best = null;

            for (int i = 0; i < lists.Length; i++)
            {
                var list = lists[i];
                if (list == null) continue;
                for (int j = 0; j < list.Count; j++)
                {
                    var c = list[j];
                    if (c == null) continue;
                    if (filterMode && c.mode != targetMode) continue;

                    int score = 0;
                    if (filterMode) score += 500;

                    if (string.Equals(c.nameAnimation, name, StringComparison.OrdinalIgnoreCase))
                        score += 1000;
                    else if (!string.IsNullOrWhiteSpace(c.nameAnimation) &&
                             c.nameAnimation.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                        score += 400;
                    else
                        continue;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = c;
                    }
                }
            }

            if (best == null)
            {
                LogWarn($"[pose] not found: name={name} mode={(filterMode ? command.poseMode.ToString() : "any")}");
                return;
            }

            PoseContinuationState continuation = CapturePoseContinuation(proc, best);
            proc.flags.selectAnimationListInfo = best;
            proc.flags.click = continuation != null && continuation.TargetHoushi && continuation.ShouldContinue
                ? HFlag.ClickKind.insert
                : HFlag.ClickKind.actionChange;

            if (continuation != null && (continuation.ShouldContinue || continuation.ShouldRestoreAuto))
            {
                SchedulePoseContinuation(continuation);
            }

            Log($"[pose] → id={best.id} mode={best.mode} name={best.nameAnimation} click={proc.flags.click} continue={(continuation != null && continuation.ShouldContinue)}");
        }

        private void HandleCameraPresetCommand(ExternalVoiceFaceCommand command)
        {
            string presetName = (command.cameraPresetName ?? string.Empty).Trim();
            int presetIndex = command.cameraPresetIndex;
            bool hasName = !string.IsNullOrEmpty(presetName);
            bool hasIndex = presetIndex >= 0;
            if (!hasName && !hasIndex)
            {
                LogWarn("[camera_preset] cameraPresetName / cameraPresetIndex is empty");
                return;
            }

            string reason;
            bool ok;
            if (hasName)
            {
                ok = TryLoadCameraPresetByNameExternal(presetName, out reason);
                if (ok)
                    Log($"[camera_preset] apply name='{presetName}'");
                else
                    LogWarn($"[camera_preset] apply failed name='{presetName}' reason={reason}");
                return;
            }

            ok = TryLoadCameraPresetByIndexExternal(presetIndex, out reason);
            if (ok)
                Log($"[camera_preset] apply index={presetIndex}");
            else
                LogWarn($"[camera_preset] apply failed index={presetIndex} reason={reason}");
        }

        private static bool TryLoadCameraPresetByNameExternal(string presetName, out string reason)
        {
            object[] args = { presetName, null };
            return InvokeMainGameCameraControlApi("TryLoadPresetByName", args, out reason);
        }

        private static bool TryLoadCameraPresetByIndexExternal(int presetIndex, out string reason)
        {
            object[] args = { presetIndex, null };
            return InvokeMainGameCameraControlApi("TryLoadPresetByIndex", args, out reason);
        }

        private static bool InvokeMainGameCameraControlApi(string methodName, object[] args, out string reason)
        {
            reason = string.Empty;
            try
            {
                Type apiType = Type.GetType("MainGameCameraControl.MainGameCameraControlApi, MainGameCameraControl", throwOnError: false);
                if (apiType == null)
                {
                    reason = "api_type_not_found";
                    return false;
                }

                var method = apiType.GetMethod(methodName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (method == null)
                {
                    reason = "api_method_not_found";
                    return false;
                }

                object result = method.Invoke(null, args);
                reason = args != null && args.Length >= 2 ? args[1] as string ?? string.Empty : string.Empty;
                return result is bool && (bool)result;
            }
            catch (Exception ex)
            {
                reason = "exception:" + ex.Message;
                return false;
            }
        }

        private PoseContinuationState CapturePoseContinuation(HSceneProc proc, HSceneProc.AnimationListInfo target)
        {
            if (proc == null || proc.flags == null || target == null)
            {
                return null;
            }

            HFlag.EMode previousMode = proc.flags.mode;
            bool previousSonyu = IsSonyuPoseMode(previousMode);
            bool previousHoushi = IsHoushiPoseMode(previousMode);
            bool targetSonyu = IsSonyuPoseMode(target.mode);
            bool targetHoushi = IsHoushiPoseMode(target.mode);
            if ((!previousSonyu || !targetSonyu) && (!previousHoushi || !targetHoushi))
            {
                return null;
            }

            HActionBase currentAction = ResolveCurrentHAction(proc);
            bool wasAuto = CapturePoseAutoEnabled(currentAction, previousSonyu, previousHoushi);

            string stateName = (proc.flags.nowAnimStateName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(stateName) && !wasAuto)
            {
                return null;
            }

            bool wasLoop = false;
            bool wasStrongLoop = false;
            bool wasAnal = false;
            bool shouldContinue = false;

            if (previousSonyu)
            {
                shouldContinue = IsSonyuContinuationState(stateName);
                wasLoop = IsSonyuLoopState(stateName);
                wasStrongLoop = IsStrongLoopState(stateName);
                wasAnal = stateName.StartsWith("A_", StringComparison.Ordinal);
            }
            else if (previousHoushi)
            {
                shouldContinue = IsHoushiContinuationState(stateName);
                wasLoop = IsHoushiLoopState(stateName);
                wasStrongLoop = IsStrongLoopState(stateName);
            }

            if (!shouldContinue && !wasAuto)
            {
                return null;
            }

            return new PoseContinuationState
            {
                ShouldContinue = shouldContinue,
                ShouldRestoreAuto = wasAuto,
                PreviousMode = previousMode,
                TargetMode = target.mode,
                TargetId = target.id,
                TargetName = target.nameAnimation ?? string.Empty,
                PreviousStateName = stateName,
                TargetSonyu = targetSonyu,
                TargetHoushi = targetHoushi,
                WasAnal = wasAnal,
                WasLoop = wasLoop,
                WasStrongLoop = wasStrongLoop,
                SpeedCalc = Mathf.Clamp01(proc.flags.speedCalc),
                SpeedHoushi = proc.flags.speedHoushi,
                VoiceSpeedMotion = proc.flags.voice != null && proc.flags.voice.speedMotion,
                WasAuto = wasAuto
            };
        }

        private void SchedulePoseContinuation(PoseContinuationState continuation)
        {
            if (continuation == null || (!continuation.ShouldContinue && !continuation.ShouldRestoreAuto))
            {
                return;
            }

            _delayedActions.Add(Tuple.Create(Time.unscaledTime + 0.12f, (Action)(() =>
            {
                ApplyPoseContinuation(continuation);
            })));

            Log($"[pose-continue] scheduled prevMode={continuation.PreviousMode} prevState={continuation.PreviousStateName} targetMode={continuation.TargetMode} target='{continuation.TargetName}' speedCalc={continuation.SpeedCalc:F2} auto={continuation.WasAuto}");
        }

        private void ApplyPoseContinuation(PoseContinuationState continuation)
        {
            if (continuation == null || (!continuation.ShouldContinue && !continuation.ShouldRestoreAuto))
            {
                return;
            }

            HSceneProc proc = CurrentProc ?? FindCurrentProc();
            if (!IsPoseContinuationTargetReady(proc, continuation))
            {
                RetryPoseContinuation(continuation, "target-not-ready");
                return;
            }

            HActionBase action = ResolveCurrentHAction(proc);
            if (action == null)
            {
                RetryPoseContinuation(continuation, "action-not-found");
                return;
            }

            if (!continuation.ShouldContinue)
            {
                RestorePoseAutoState(action, continuation);
                return;
            }

            if (continuation.TargetSonyu)
            {
                ApplySonyuPoseContinuation(proc, action, continuation);
                return;
            }

            if (continuation.TargetHoushi)
            {
                ApplyHoushiPoseContinuation(proc, action, continuation);
            }
        }

        private void RetryPoseContinuation(PoseContinuationState continuation, string reason)
        {
            continuation.Attempts++;
            if (continuation.Attempts >= 16)
            {
                LogWarn($"[pose-continue] give up reason={reason} target='{continuation.TargetName}' attempts={continuation.Attempts}");
                return;
            }

            _delayedActions.Add(Tuple.Create(Time.unscaledTime + 0.08f, (Action)(() =>
            {
                ApplyPoseContinuation(continuation);
            })));
        }

        private static bool IsPoseContinuationTargetReady(HSceneProc proc, PoseContinuationState continuation)
        {
            if (proc == null || proc.flags == null || continuation == null)
            {
                return false;
            }

            HSceneProc.AnimationListInfo info = proc.flags.nowAnimationInfo;
            if (info == null)
            {
                return false;
            }

            if (info.mode != continuation.TargetMode)
            {
                return false;
            }

            if (info.id == continuation.TargetId)
            {
                return true;
            }

            return string.Equals(info.nameAnimation, continuation.TargetName, StringComparison.OrdinalIgnoreCase);
        }

        private static HActionBase ResolveCurrentHAction(HSceneProc proc)
        {
            if (proc == null || proc.flags == null || LstProcField == null)
            {
                return null;
            }

            try
            {
                IList actions = LstProcField.GetValue(proc) as IList;
                int mode = (int)proc.flags.mode;
                if (actions == null || mode < 0 || mode >= actions.Count)
                {
                    return null;
                }

                return actions[mode] as HActionBase;
            }
            catch
            {
                return null;
            }
        }

        private void ApplySonyuPoseContinuation(HSceneProc proc, HActionBase action, PoseContinuationState continuation)
        {
            bool useAnal = continuation.WasAnal && proc.flags.isAnalInsertOK;
            string stateName = BuildSonyuContinuationStateName(continuation, useAnal);
            RestorePoseContinuationSpeed(proc, continuation, minLoopSpeed: continuation.WasLoop ? 0.35f : 0f);
            proc.flags.finish = HFlag.FinishKind.none;
            proc.flags.voiceWait = false;
            proc.flags.isAnalPlay = useAnal;
            if (useAnal)
            {
                proc.flags.SetInsertAnal();
                proc.flags.SetInsertAnalVoiceCondition();
            }
            else
            {
                proc.flags.SetInsertKokan();
                proc.flags.SetInsertKokanVoiceCondition();
            }

            proc.flags.click = HFlag.ClickKind.none;
            action.SetPlay(stateName);
            RestorePoseAutoState(action, continuation);
            Log($"[pose-continue] applied sonyu state={stateName} target='{continuation.TargetName}' anal={useAnal} speedCalc={proc.flags.speedCalc:F2}");
        }

        private void ApplyHoushiPoseContinuation(HSceneProc proc, HActionBase action, PoseContinuationState continuation)
        {
            string stateName = continuation.WasStrongLoop ? "SLoop" : "WLoop";
            RestorePoseContinuationSpeed(proc, continuation, minLoopSpeed: 0.35f);
            proc.flags.finish = HFlag.FinishKind.none;
            proc.flags.voiceWait = false;
            proc.flags.speedHoushi = continuation.SpeedHoushi;
            proc.flags.SetHoushiPlay();
            proc.flags.click = HFlag.ClickKind.none;
            action.SetPlay(stateName);
            RestorePoseAutoState(action, continuation);
            Log($"[pose-continue] applied houshi state={stateName} target='{continuation.TargetName}' speedCalc={proc.flags.speedCalc:F2}");
        }

        private static bool CapturePoseAutoEnabled(HActionBase action, bool sonyuMode, bool houshiMode)
        {
            if (action == null)
            {
                return false;
            }

            try
            {
                if (sonyuMode)
                {
                    FieldInfo isAutoField = action.GetType().GetField("isAuto", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (isAutoField != null && isAutoField.FieldType == typeof(bool))
                    {
                        return (bool)isAutoField.GetValue(action);
                    }
                }

                if (houshiMode)
                {
                    FieldInfo autoStartField = action.GetType().GetField("autoStart", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (autoStartField != null && autoStartField.FieldType == typeof(bool))
                    {
                        return (bool)autoStartField.GetValue(action);
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private void RestorePoseAutoState(HActionBase action, PoseContinuationState continuation)
        {
            if (action == null || continuation == null || !continuation.ShouldRestoreAuto || !continuation.WasAuto)
            {
                return;
            }

            try
            {
                if (continuation.TargetSonyu)
                {
                    FieldInfo isAutoField = action.GetType().GetField("isAuto", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (isAutoField != null && isAutoField.FieldType == typeof(bool))
                    {
                        isAutoField.SetValue(action, true);
                        Log($"[pose-auto] restored sonyu auto target='{continuation.TargetName}'");
                    }

                    return;
                }

                if (continuation.TargetHoushi)
                {
                    FieldInfo autoStartField = action.GetType().GetField("autoStart", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (autoStartField != null && autoStartField.FieldType == typeof(bool))
                    {
                        autoStartField.SetValue(action, true);
                        Log($"[pose-auto] restored houshi auto target='{continuation.TargetName}'");
                    }
                }
            }
            catch (Exception ex)
            {
                LogWarn($"[pose-auto] restore failed target='{continuation.TargetName}' message={ex.Message}");
            }
        }

        private static void RestorePoseContinuationSpeed(HSceneProc proc, PoseContinuationState continuation, float minLoopSpeed)
        {
            if (proc == null || proc.flags == null || continuation == null)
            {
                return;
            }

            float speedCalc = Mathf.Clamp01(Mathf.Max(continuation.SpeedCalc, minLoopSpeed));
            proc.flags.speedCalc = speedCalc;
            proc.flags.speedMaxBody = 1f;
            proc.flags.speed = ResolvePoseContinuationSpeed(proc.flags, speedCalc);
            if (proc.flags.voice != null)
            {
                proc.flags.voice.speedMotion = continuation.VoiceSpeedMotion || speedCalc >= 0.6f;
            }
        }

        private static float ResolvePoseContinuationSpeed(HFlag flags, float speedCalc)
        {
            if (flags == null)
            {
                return speedCalc;
            }

            AnimationCurve curve = IsHoushiPoseMode(flags.mode) ? flags.speedHoushiCurve : flags.speedSonyuCurve;
            return curve != null ? curve.Evaluate(speedCalc) : speedCalc;
        }

        private static string BuildSonyuContinuationStateName(PoseContinuationState continuation, bool useAnal)
        {
            string prefix = useAnal ? "A_" : string.Empty;
            if (!continuation.WasLoop)
            {
                return prefix + "InsertIdle";
            }

            return prefix + (continuation.WasStrongLoop ? "SLoop" : "WLoop");
        }

        private static bool IsSonyuPoseMode(HFlag.EMode mode)
        {
            return mode == HFlag.EMode.sonyu || mode == HFlag.EMode.sonyu3P || mode == HFlag.EMode.sonyu3PMMF;
        }

        private static bool IsHoushiPoseMode(HFlag.EMode mode)
        {
            return mode == HFlag.EMode.houshi || mode == HFlag.EMode.houshi3P || mode == HFlag.EMode.houshi3PMMF;
        }

        private static bool IsSonyuContinuationState(string stateName)
        {
            return IsSonyuLoopState(stateName)
                || string.Equals(stateName, "Insert", StringComparison.Ordinal)
                || string.Equals(stateName, "A_Insert", StringComparison.Ordinal)
                || string.Equals(stateName, "InsertIdle", StringComparison.Ordinal)
                || string.Equals(stateName, "A_InsertIdle", StringComparison.Ordinal);
        }

        private static bool IsSonyuLoopState(string stateName)
        {
            return string.Equals(stateName, "WLoop", StringComparison.Ordinal)
                || string.Equals(stateName, "SLoop", StringComparison.Ordinal)
                || string.Equals(stateName, "A_WLoop", StringComparison.Ordinal)
                || string.Equals(stateName, "A_SLoop", StringComparison.Ordinal);
        }

        private static bool IsHoushiContinuationState(string stateName)
        {
            return IsHoushiLoopState(stateName);
        }

        private static bool IsHoushiLoopState(string stateName)
        {
            return string.Equals(stateName, "WLoop", StringComparison.Ordinal)
                || string.Equals(stateName, "SLoop", StringComparison.Ordinal);
        }

        private static bool IsStrongLoopState(string stateName)
        {
            return string.Equals(stateName, "SLoop", StringComparison.Ordinal)
                || string.Equals(stateName, "A_SLoop", StringComparison.Ordinal);
        }

        // ----------------------------------------------------------------
        // clothes コマンド: 各部位の着衣状態を変える
        // ----------------------------------------------------------------

        private void HandleClothesCommand(ExternalVoiceFaceCommand command)
        {
            if (command.clothesItems == null || command.clothesItems.Length == 0)
            {
                LogWarn("[clothes] clothesItems が空です");
                return;
            }

            ChaControl female = null;
            HSceneProc proc = FindCurrentProc();
            if (proc != null)
            {
                int mainForResolve = ClampMainIndex(proc, command.ResolveMain(Settings?.TargetMainIndex ?? 0));
                female = ResolveFemale(proc, mainForResolve);
            }
            if (female == null)
            {
                female = UnityEngine.Object.FindObjectsOfType<ChaControl>()
                    .FirstOrDefault(c => c != null && c.sex == 1 && c.visibleAll);
            }
            if (female == null)
            {
                LogWarn("[clothes] ChaControl が見つかりません");
                return;
            }

            foreach (var item in command.clothesItems)
            {
                if (item.kind < 0 || item.kind > 8) continue;
                try
                {
                    if (item.state < 0)
                    {
                        ApplyShiftClothesState(female, item.kind);
                    }
                    else
                    {
                        female.SetClothesState(item.kind, (byte)item.state);
                        Log($"[clothes] SetClothesState kind={item.kind} state={item.state}");
                    }
                }
                catch (Exception ex)
                {
                    LogWarn($"[clothes] 失敗 kind={item.kind}: {ex.Message}");
                }
            }
        }

        private void HandleGlassesToggle(int main, bool show)
        {
            ChaControl female = ResolveFemaleForMain(main);
            if (female == null)
            {
                LogWarn("[accessory][glasses] ChaControl が見つかりません");
                return;
            }

            int[] slots = FindGlassesAccessorySlots(female);
            if (slots.Length == 0)
            {
                LogWarn("[accessory][glasses] glasses slot not found (parentKey=a_n_megane)");
                return;
            }

            int changed = 0;
            int already = 0;
            int failed = 0;

            foreach (int slot in slots)
            {
                try
                {
                    bool before = TryGetAccessoryShowState(female, slot, out bool b) ? b : false;
                    female.SetAccessoryState(slot, show);
                    bool after = TryGetAccessoryShowState(female, slot, out bool a) ? a : show;

                    if (before == show)
                    {
                        already++;
                    }
                    else if (after == show)
                    {
                        changed++;
                    }
                    else
                    {
                        failed++;
                    }

                    Log($"[accessory][glasses] slot={slot} show={(show ? 1 : 0)} before={(before ? 1 : 0)} after={(after ? 1 : 0)}");
                }
                catch (Exception ex)
                {
                    failed++;
                    LogWarn($"[accessory][glasses] failed slot={slot} show={(show ? 1 : 0)} message={ex.Message}");
                }
            }

            Log($"[accessory][glasses] apply show={(show ? 1 : 0)} slots={slots.Length} changed={changed} already={already} failed={failed}");
        }

        private ChaControl ResolveFemaleForMain(int main)
        {
            HSceneProc proc = FindCurrentProc();
            if (proc != null)
            {
                int mainForResolve = ClampMainIndex(proc, main);
                ChaControl female = ResolveFemale(proc, mainForResolve);
                if (female != null)
                {
                    return female;
                }
            }

            return UnityEngine.Object.FindObjectsOfType<ChaControl>()
                .FirstOrDefault(c => c != null && c.sex == 1 && c.visibleAll);
        }

        private static int[] FindGlassesAccessorySlots(ChaControl female)
        {
            var parts = female?.nowCoordinate?.accessory?.parts;
            if (parts == null || parts.Length == 0)
            {
                return new int[0];
            }

            var slots = new List<int>(4);
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (part == null || part.type == 120)
                {
                    continue;
                }

                string parentKey = (part.parentKey ?? string.Empty).Trim();
                if (parentKey.Equals("a_n_megane", StringComparison.OrdinalIgnoreCase) ||
                    parentKey.IndexOf("megane", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    slots.Add(i);
                }
            }

            return slots.ToArray();
        }

        private static bool TryGetAccessoryShowState(ChaControl female, int slot, out bool show)
        {
            show = false;
            try
            {
                bool[] states = female?.fileStatus?.showAccessory;
                if (states == null || slot < 0 || slot >= states.Length)
                {
                    return false;
                }

                show = states[slot];
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void ApplyShiftClothesState(ChaControl female, int kind)
        {
            byte currentState = 0;
            try
            {
                if (female?.fileStatus?.clothesState != null && kind >= 0 && kind < female.fileStatus.clothesState.Length)
                {
                    currentState = female.fileStatus.clothesState[kind];
                }
            }
            catch
            {
                currentState = 0;
            }

            byte targetState = ResolveShiftTargetState(female, kind, currentState);
            female.SetClothesState(kind, targetState, next: false);
            Log($"[clothes] Shift kind={kind} current={currentState} target={targetState}");
        }

        private static byte ResolveShiftTargetState(ChaControl female, int kind, byte currentState)
        {
            bool hasState1 = female != null && female.IsClothesStateType(kind, 1);
            bool hasState2 = female != null && female.IsClothesStateType(kind, 2);

            if (!hasState1 && !hasState2)
            {
                return currentState <= 3 ? currentState : (byte)0;
            }

            if (currentState <= 0)
            {
                return hasState1 ? (byte)1 : (byte)2;
            }

            if (currentState == 1)
            {
                return hasState2 ? (byte)2 : (byte)1;
            }

            if (currentState == 2)
            {
                return 2;
            }

            return hasState2 ? (byte)2 : (byte)1;
        }

        // ----------------------------------------------------------------
        // coord コマンド: 衣装名で検索して着替える
        // ----------------------------------------------------------------

        private static readonly string[] CoordTypeNames = { "plain", "swim", "pajamas", "bathing" };

        private void HandleCoordCommand(ExternalVoiceFaceCommand command)
        {
            string coordName = (command.coordName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(coordName))
            {
                LogWarn("[coord] coordName が空です");
                return;
            }

            // ChaControl を取得: HScene → フォールバック
            ChaControl female = null;
            HSceneProc proc = FindCurrentProc();
            if (proc != null)
            {
                int resolvedMain = ClampMainIndex(proc, command.ResolveMain(Settings?.TargetMainIndex ?? 0));
                female = ResolveFemale(proc, resolvedMain);
            }
            if (female == null)
            {
                female = UnityEngine.Object.FindObjectsOfType<ChaControl>()
                    .FirstOrDefault(c => c != null && c.sex == 1 && c.visibleAll);
            }
            if (female == null)
            {
                LogWarn("[coord] ChaControl が見つかりません");
                return;
            }

            int main = command.ResolveMain(Settings?.TargetMainIndex ?? 0);
            int? beforeCoord = TryGetCurrentCoordinateIndex(female);
            Log($"[coord] request name='{coordName}' main={main} before={(beforeCoord.HasValue ? beforeCoord.Value.ToString() : "unknown")}");

            string matchedSlotName;
            bool exactMatch;
            int coordIndex = TryFindCoordIndexByName(female, coordName, out matchedSlotName, out exactMatch);
            if (coordIndex < 0)
            {
                LogWarn($"[coord] '{coordName}' に一致するコーデが見つかりません");
                LogWarn($"[coord] slots: {BuildCoordSlotSummary(female)}");
                return;
            }

            try
            {
                Log($"[coord] match name='{coordName}' slot={coordIndex} slotName='{matchedSlotName}' match={(exactMatch ? "exact" : "partial")}");
                female.ChangeCoordinateTypeAndReload((ChaFileDefine.CoordinateType)coordIndex, true);
                int? immediateCoord = TryGetCurrentCoordinateIndex(female);
                Log($"[coord] apply immediate target={coordIndex} current={(immediateCoord.HasValue ? immediateCoord.Value.ToString() : "unknown")}");
                ScheduleCoordResultCheck(female, coordName, coordIndex);
            }
            catch (Exception ex)
            {
                LogWarn($"[coord] ChangeCoordinateTypeAndReload 失敗: {ex.Message}");
            }
        }

        private static int TryFindCoordIndexByName(ChaControl female, string name, out string matchedSlotName, out bool exactMatch)
        {
            matchedSlotName = string.Empty;
            exactMatch = false;
            string nameLower = name.ToLowerInvariant();
            var coordSlots = female.chaFile?.coordinate;
            if (coordSlots == null) return -1;

            // MoreOutfitsController のリフレクション準備
            MonoBehaviour moCtrl = null;
            MethodInfo moGetName = null;
            try
            {
                moCtrl = female.gameObject.GetComponents<MonoBehaviour>()
                    .FirstOrDefault(c => c.GetType().Name == "MoreOutfitsController");
                if (moCtrl != null)
                    moGetName = moCtrl.GetType().GetMethod("GetCoodinateName",
                        BindingFlags.Public | BindingFlags.Instance);
            }
            catch { }

            // 完全一致を優先、次に部分一致
            int partialMatch = -1;
            string partialSlotName = string.Empty;
            for (int i = 0; i < coordSlots.Length; i++)
            {
                string rawSlotName = GetCoordSlotName(i, moCtrl, moGetName);
                string slotName = rawSlotName.ToLowerInvariant();
                if (slotName == nameLower)
                {
                    matchedSlotName = rawSlotName;
                    exactMatch = true;
                    return i;
                }
                if (partialMatch < 0 && (slotName.Contains(nameLower) || nameLower.Contains(slotName)))
                {
                    partialMatch = i;
                    partialSlotName = rawSlotName;
                }
            }

            matchedSlotName = partialSlotName;
            return partialMatch;
        }

        private void ScheduleCoordResultCheck(ChaControl female, string coordName, int targetIndex)
        {
            if (female == null)
            {
                return;
            }

            _delayedActions.Add(Tuple.Create(Time.time + 0.8f, (Action)(() =>
            {
                int? current = TryGetCurrentCoordinateIndex(female);
                string result;
                if (!current.HasValue)
                {
                    result = "unknown";
                }
                else if (current.Value == targetIndex)
                {
                    result = "ok";
                }
                else
                {
                    result = "overwritten_or_failed";
                }

                Log($"[coord] post-check name='{coordName}' target={targetIndex} current={(current.HasValue ? current.Value.ToString() : "unknown")} result={result}");
            })));
        }

        private static int? TryGetCurrentCoordinateIndex(ChaControl female)
        {
            if (female == null)
            {
                return null;
            }

            try
            {
                object fileStatus = female.fileStatus;
                int value;
                if (TryReadIntMember(fileStatus, "coordinateType", out value)) return value;
                if (TryReadIntMember(fileStatus, "nowCoordinateType", out value)) return value;
                if (TryReadIntMember(fileStatus, "coordType", out value)) return value;
            }
            catch
            {
            }

            try
            {
                int value;
                if (TryReadIntMember(female, "coordinateType", out value)) return value;
                if (TryReadIntMember(female, "nowCoordinateType", out value)) return value;
                if (TryReadIntMember(female, "coordType", out value)) return value;
            }
            catch
            {
            }

            return null;
        }

        private static bool TryReadIntMember(object target, string memberName, out int value)
        {
            value = 0;
            if (target == null || string.IsNullOrWhiteSpace(memberName))
            {
                return false;
            }

            try
            {
                Type type = target.GetType();
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                PropertyInfo prop = type.GetProperty(memberName, flags);
                if (prop != null)
                {
                    object v = prop.GetValue(target, null);
                    if (v is int)
                    {
                        value = (int)v;
                        return true;
                    }
                    if (v is byte)
                    {
                        value = (byte)v;
                        return true;
                    }
                }

                FieldInfo field = type.GetField(memberName, flags);
                if (field != null)
                {
                    object v = field.GetValue(target);
                    if (v is int)
                    {
                        value = (int)v;
                        return true;
                    }
                    if (v is byte)
                    {
                        value = (byte)v;
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static string BuildCoordSlotSummary(ChaControl female)
        {
            try
            {
                var coordSlots = female?.chaFile?.coordinate;
                if (coordSlots == null || coordSlots.Length == 0)
                {
                    return "(empty)";
                }

                MonoBehaviour moCtrl = null;
                MethodInfo moGetName = null;
                try
                {
                    moCtrl = female.gameObject.GetComponents<MonoBehaviour>()
                        .FirstOrDefault(c => c.GetType().Name == "MoreOutfitsController");
                    if (moCtrl != null)
                    {
                        moGetName = moCtrl.GetType().GetMethod("GetCoodinateName",
                            BindingFlags.Public | BindingFlags.Instance);
                    }
                }
                catch
                {
                }

                int max = Math.Min(coordSlots.Length, 20);
                var list = new List<string>(max);
                for (int i = 0; i < max; i++)
                {
                    string n = GetCoordSlotName(i, moCtrl, moGetName);
                    list.Add(i + ":" + n);
                }

                if (coordSlots.Length > max)
                {
                    list.Add("...");
                }

                return string.Join(", ", list.ToArray());
            }
            catch
            {
                return "(error)";
            }
        }

        private static string GetCoordSlotName(int index, MonoBehaviour moCtrl, MethodInfo moGetName)
        {
            if (index < CoordTypeNames.Length) return CoordTypeNames[index];
            if (moCtrl != null && moGetName != null)
            {
                try { return (moGetName.Invoke(moCtrl, new object[] { index }) as string) ?? index.ToString(); }
                catch { }
            }
            return index.ToString();
        }

        private string NormalizeAudioPath(string incomingPath)
        {
            if (string.IsNullOrWhiteSpace(incomingPath))
            {
                return string.Empty;
            }

            try
            {
                string path = incomingPath.Trim().Trim('"');
                if (!Path.IsPathRooted(path))
                {
                    path = Path.Combine(PluginDir ?? string.Empty, path);
                }

                return Path.GetFullPath(path);
            }
            catch (Exception ex)
            {
                LogWarn("normalize audio path failed: " + ex.Message);
                return string.Empty;
            }
        }

        private static string ResolveFacePresetJsonPath(PluginSettings settings)
        {
            string configuredPath = settings == null || string.IsNullOrWhiteSpace(settings.FacePresetJsonRelativePath)
                ? DefaultFacePresetJsonRelativePath
                : settings.FacePresetJsonRelativePath.Trim();

            try
            {
                if (Path.IsPathRooted(configuredPath))
                {
                    return Path.GetFullPath(configuredPath);
                }

                return Path.GetFullPath(Path.Combine(PluginDir ?? string.Empty, configuredPath));
            }
            catch
            {
                return configuredPath;
            }
        }

        private bool TryLoadFacePresetItems(string sourcePath, out List<FacePresetJsonItem> items, out string reason)
        {
            items = new List<FacePresetJsonItem>();
            reason = string.Empty;
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                reason = "path_empty";
                return false;
            }

            try
            {
                var info = new FileInfo(sourcePath);
                if (!info.Exists)
                {
                    reason = "file_not_found";
                    return false;
                }

                DateTime writeTime = info.LastWriteTimeUtc;
                long length = info.Length;

                lock (_facePresetCacheLock)
                {
                    bool cacheHit =
                        string.Equals(_facePresetCachePath, sourcePath, StringComparison.OrdinalIgnoreCase)
                        && _facePresetCacheWriteTimeUtc == writeTime
                        && _facePresetCacheLength == length
                        && _facePresetCache != null
                        && _facePresetCache.Count > 0;
                    if (cacheHit)
                    {
                        items = _facePresetCache.Select(CloneFacePresetItem).Where(p => p != null).ToList();
                        reason = "cache_hit";
                        return items.Count > 0;
                    }
                }

                string json = File.ReadAllText(sourcePath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                {
                    reason = "json_empty";
                    return false;
                }

                FacePresetJsonFile parsed = DeserializeFacePresetJson(json);
                if (parsed == null || parsed.Presets == null)
                {
                    reason = "parse_null";
                    return false;
                }

                var normalized = new List<FacePresetJsonItem>(parsed.Presets.Count);
                for (int i = 0; i < parsed.Presets.Count; i++)
                {
                    FacePresetJsonItem normalizedItem = NormalizeFacePresetItem(parsed.Presets[i]);
                    if (normalizedItem == null)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(normalizedItem.Name) && string.IsNullOrWhiteSpace(normalizedItem.Id))
                    {
                        continue;
                    }

                    normalized.Add(normalizedItem);
                }

                if (normalized.Count <= 0)
                {
                    reason = "preset_empty";
                    return false;
                }

                lock (_facePresetCacheLock)
                {
                    _facePresetCachePath = sourcePath;
                    _facePresetCacheWriteTimeUtc = writeTime;
                    _facePresetCacheLength = length;
                    _facePresetCache = normalized.Select(CloneFacePresetItem).Where(p => p != null).ToList();
                }

                items = normalized.Select(CloneFacePresetItem).Where(p => p != null).ToList();
                reason = "loaded";
                return items.Count > 0;
            }
            catch (Exception ex)
            {
                reason = "load_exception:" + ex.Message;
                return false;
            }
        }

        private static FacePresetJsonFile DeserializeFacePresetJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(json);
            using (var ms = new MemoryStream(bytes))
            {
                var serializer = new DataContractJsonSerializer(typeof(FacePresetJsonFile));
                return serializer.ReadObject(ms) as FacePresetJsonFile;
            }
        }

        private static FacePresetJsonItem NormalizeFacePresetItem(FacePresetJsonItem source)
        {
            if (source == null)
            {
                return null;
            }

            var normalized = new FacePresetJsonItem
            {
                Id = (source.Id ?? string.Empty).Trim(),
                Name = (source.Name ?? string.Empty).Trim(),
                Eyebrow = Mathf.Max(0, source.Eyebrow),
                Eye = Mathf.Max(0, source.Eye),
                Mouth = Mathf.Max(0, source.Mouth),
                EyeMin = Mathf.Clamp01(source.EyeMin),
                MouthMin = Mathf.Clamp01(source.MouthMin),
                Tears = Mathf.Clamp(source.Tears, 0, 10),
                Cheek = Mathf.Clamp01(source.Cheek)
            };

            normalized.EyeMax = Mathf.Clamp01(Mathf.Max(normalized.EyeMin, source.EyeMax));
            normalized.MouthMax = Mathf.Clamp01(Mathf.Max(normalized.MouthMin, source.MouthMax));
            return normalized;
        }

        private static FacePresetJsonItem CloneFacePresetItem(FacePresetJsonItem source)
        {
            if (source == null)
            {
                return null;
            }

            return new FacePresetJsonItem
            {
                Id = source.Id,
                Name = source.Name,
                Eyebrow = source.Eyebrow,
                Eye = source.Eye,
                Mouth = source.Mouth,
                EyeMin = source.EyeMin,
                EyeMax = source.EyeMax,
                MouthMin = source.MouthMin,
                MouthMax = source.MouthMax,
                Tears = source.Tears,
                Cheek = source.Cheek
            };
        }

        private bool TrySelectFacePresetByRoute(
            PluginSettings settings,
            string requestedName,
            string requestedId,
            bool facePresetRandom,
            out FacePresetJsonItem selected,
            out string sourcePath,
            out int poolCount,
            out int candidateCount,
            out string reason)
        {
            selected = null;
            sourcePath = ResolveFacePresetJsonPath(settings);
            poolCount = 0;
            candidateCount = 0;
            reason = string.Empty;

            List<FacePresetJsonItem> allPresets;
            string loadReason;
            if (!TryLoadFacePresetItems(sourcePath, out allPresets, out loadReason))
            {
                reason = "load_failed:" + loadReason;
                return false;
            }

            poolCount = allPresets.Count;
            string name = (requestedName ?? string.Empty).Trim();
            string id = (requestedId ?? string.Empty).Trim();

            var candidates = new List<FacePresetJsonItem>();
            if (!string.IsNullOrWhiteSpace(id))
            {
                for (int i = 0; i < allPresets.Count; i++)
                {
                    FacePresetJsonItem item = allPresets[i];
                    if (item != null && string.Equals(item.Id ?? string.Empty, id, StringComparison.OrdinalIgnoreCase))
                    {
                        candidates.Add(item);
                    }
                }
            }

            if (candidates.Count <= 0 && !string.IsNullOrWhiteSpace(name))
            {
                for (int i = 0; i < allPresets.Count; i++)
                {
                    FacePresetJsonItem item = allPresets[i];
                    if (item != null && string.Equals(item.Name ?? string.Empty, name, StringComparison.OrdinalIgnoreCase))
                    {
                        candidates.Add(item);
                    }
                }
            }

            if (candidates.Count <= 0 && facePresetRandom && string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(id))
            {
                candidates.AddRange(allPresets);
            }

            candidateCount = candidates.Count;
            if (candidateCount <= 0)
            {
                reason = "candidate_not_found";
                return false;
            }

            int index = 0;
            if (facePresetRandom && candidateCount > 1)
            {
                lock (_random)
                {
                    index = _random.Next(candidateCount);
                }
            }

            selected = CloneFacePresetItem(candidates[index]);
            if (selected == null)
            {
                reason = "candidate_null";
                return false;
            }

            reason = "selected";
            return true;
        }

        private bool TryApplyFacePresetByRoute(
            PluginSettings settings,
            ChaControl female,
            string requestedName,
            string requestedId,
            bool facePresetRandom,
            out FacePresetJsonItem selected,
            out string sourcePath,
            out int poolCount,
            out int candidateCount,
            out string reason)
        {
            selected = null;
            sourcePath = ResolveFacePresetJsonPath(settings);
            poolCount = 0;
            candidateCount = 0;
            reason = string.Empty;

            if (female == null)
            {
                reason = "female_null";
                return false;
            }

            FacePresetJsonItem resolvedPreset;
            if (!TrySelectFacePresetByRoute(
                    settings,
                    requestedName,
                    requestedId,
                    facePresetRandom,
                    out resolvedPreset,
                    out sourcePath,
                    out poolCount,
                    out candidateCount,
                    out reason))
            {
                return false;
            }

            try
            {
                ApplyFacePresetToCha(female, resolvedPreset);
                selected = resolvedPreset;
                reason = "applied";
                return true;
            }
            catch (Exception ex)
            {
                selected = resolvedPreset;
                reason = "apply_exception:" + ex.Message;
                return false;
            }
        }

        private static void ApplyFacePresetToCha(ChaControl cha, FacePresetJsonItem preset)
        {
            if (cha == null || preset == null)
            {
                return;
            }

            cha.ChangeEyebrowPtn(preset.Eyebrow, true);
            cha.ChangeEyesPtn(preset.Eye, true);
            cha.ChangeMouthPtn(preset.Mouth, true);

            float eyeMin = Mathf.Clamp01(preset.EyeMin);
            float eyeMax = Mathf.Clamp01(Mathf.Max(eyeMin, preset.EyeMax));
            cha.ChangeEyesOpenMax(eyeMax);
            SetFaceCtrlMinMax(cha, "eyesCtrl", eyeMin, eyeMax);

            float mouthMin = Mathf.Clamp01(preset.MouthMin);
            float mouthMax = Mathf.Clamp01(Mathf.Max(mouthMin, preset.MouthMax));
            cha.ChangeMouthOpenMax(mouthMax);
            SetFaceCtrlMinMax(cha, "mouthCtrl", mouthMin, mouthMax);

            if (cha.mouthCtrl != null)
            {
                cha.mouthCtrl.FixedRate = mouthMin;
            }

            cha.tearsLv = (byte)Mathf.Clamp(preset.Tears, 0, 10);
            cha.ChangeHohoAkaRate(Mathf.Clamp01(preset.Cheek));
        }

        internal void OnFacePresetProbeMutatorCalled(ChaControl target, string methodName, string arg0)
        {
            var probe = _facePresetProbe;
            if (probe == null || target == null || probe.Target == null)
            {
                return;
            }

            if (!ReferenceEquals(target, probe.Target))
            {
                return;
            }

            int frame = Time.frameCount;
            if (frame > probe.ExpireFrame)
            {
                return;
            }

            string caller = ResolveFacePresetProbeCaller();
            string hookKey = methodName + "|" + arg0 + "|" + caller;
            if (probe.LastHookFrame == frame && string.Equals(probe.LastHookKey, hookKey, StringComparison.Ordinal))
            {
                return;
            }

            probe.LastHookFrame = frame;
            probe.LastHookKey = hookKey;

            if (probe.HookLogCount >= 40)
            {
                return;
            }

            probe.HookLogCount++;
            Log(
                "[cmd][face-preset][probe-call]"
                + " id=" + probe.ProbeId
                + " frame=+" + (frame - probe.StartFrame)
                + " method=" + (string.IsNullOrWhiteSpace(methodName) ? "(unknown)" : methodName)
                + " arg0=" + (string.IsNullOrWhiteSpace(arg0) ? "(empty)" : arg0)
                + " caller=" + caller);
        }

        private void StartFacePresetProbe(
            ChaControl target,
            string requestedName,
            string requestedId,
            bool requestedRandom,
            string selectedName,
            string selectedId)
        {
            if (target == null)
            {
                _facePresetProbe = null;
                return;
            }

            FaceSnapshot baseline = CaptureFaceSnapshot(target);
            if (baseline == null)
            {
                _facePresetProbe = null;
                LogWarn("[cmd][face-preset][probe] start failed reason=snapshot_null");
                return;
            }

            int probeId = Interlocked.Increment(ref _facePresetProbeSequence);
            int startFrame = Time.frameCount;
            _facePresetProbe = new FacePresetProbeState
            {
                ProbeId = probeId,
                Target = target,
                RequestedName = requestedName ?? string.Empty,
                RequestedId = requestedId ?? string.Empty,
                RequestedRandom = requestedRandom,
                SelectedName = selectedName ?? string.Empty,
                SelectedId = selectedId ?? string.Empty,
                StartFrame = startFrame,
                ExpireFrame = startFrame + 240,
                StartTime = Time.realtimeSinceStartup,
                Baseline = baseline,
                Last = baseline,
                ChangeLogCount = 0,
                HookLogCount = 0,
                LastHookFrame = -1,
                LastHookKey = string.Empty
            };

            Log(
                "[cmd][face-preset][probe] start"
                + " id=" + probeId
                + " requestedName=" + (string.IsNullOrWhiteSpace(requestedName) ? "(empty)" : requestedName)
                + " requestedId=" + (string.IsNullOrWhiteSpace(requestedId) ? "(empty)" : requestedId)
                + " requestedRandom=" + requestedRandom
                + " selectedName=" + (string.IsNullOrWhiteSpace(selectedName) ? "(empty)" : selectedName)
                + " selectedId=" + (string.IsNullOrWhiteSpace(selectedId) ? "(empty)" : selectedId)
                + " baseline=" + FormatFaceSnapshot(baseline));
        }

        private void UpdateFacePresetProbe()
        {
            var probe = _facePresetProbe;
            if (probe == null)
            {
                return;
            }

            if (probe.Target == null)
            {
                LogWarn(
                    "[cmd][face-preset][probe] end"
                    + " id=" + probe.ProbeId
                    + " reason=target_null");
                _facePresetProbe = null;
                return;
            }

            int frame = Time.frameCount;
            int elapsedFrame = frame - probe.StartFrame;
            if (frame > probe.ExpireFrame)
            {
                FaceSnapshot finalSnapshot = CaptureFaceSnapshot(probe.Target);
                string finalDiff = DescribeFaceSnapshotDiff(probe.Baseline, finalSnapshot);
                Log(
                    "[cmd][face-preset][probe] end"
                    + " id=" + probe.ProbeId
                    + " reason=expired"
                    + " frames=" + elapsedFrame
                    + " changes=" + probe.ChangeLogCount
                    + " hookCalls=" + probe.HookLogCount
                    + " final=" + FormatFaceSnapshot(finalSnapshot)
                    + " baselineDiff=" + (string.IsNullOrWhiteSpace(finalDiff) ? "none" : finalDiff));
                _facePresetProbe = null;
                return;
            }

            FaceSnapshot current = CaptureFaceSnapshot(probe.Target);
            if (current == null)
            {
                return;
            }

            if (!AreFaceSnapshotsEqual(current, probe.Last))
            {
                probe.ChangeLogCount++;
                string delta = DescribeFaceSnapshotDiff(probe.Last, current);
                string baselineDiff = DescribeFaceSnapshotDiff(probe.Baseline, current);
                LogWarn(
                    "[cmd][face-preset][probe] changed"
                    + " id=" + probe.ProbeId
                    + " frame=+" + elapsedFrame
                    + " delta=" + (string.IsNullOrWhiteSpace(delta) ? "none" : delta)
                    + " baselineDiff=" + (string.IsNullOrWhiteSpace(baselineDiff) ? "none" : baselineDiff)
                    + " state=" + FormatFaceSnapshot(current));
                probe.Last = current;
                return;
            }

            if (elapsedFrame == 1 || elapsedFrame == 10 || elapsedFrame == 30 || elapsedFrame == 60 || elapsedFrame == 120 || elapsedFrame == 180)
            {
                Log(
                    "[cmd][face-preset][probe] stable"
                    + " id=" + probe.ProbeId
                    + " frame=+" + elapsedFrame
                    + " state=" + FormatFaceSnapshot(current));
            }
        }

        private static FaceSnapshot CaptureFaceSnapshot(ChaControl cha)
        {
            if (cha == null || cha.fileStatus == null)
            {
                return null;
            }

            float eyesMin = 0f;
            float eyesMax = cha.fileStatus.eyesOpenMax;
            TryReadFaceCtrlMinMax(cha, "eyesCtrl", ref eyesMin, ref eyesMax);

            float mouthMin = 0f;
            float mouthMax = cha.fileStatus.mouthOpenMax;
            TryReadFaceCtrlMinMax(cha, "mouthCtrl", ref mouthMin, ref mouthMax);

            return new FaceSnapshot
            {
                Eyebrow = cha.fileStatus.eyebrowPtn,
                Eyes = cha.fileStatus.eyesPtn,
                Mouth = cha.fileStatus.mouthPtn,
                EyesOpenMin = eyesMin,
                EyesOpenMax = eyesMax,
                MouthOpenMin = mouthMin,
                MouthOpenMax = mouthMax,
                MouthFixedRate = cha.mouthCtrl != null ? cha.mouthCtrl.FixedRate : float.NaN,
                Tears = cha.tearsLv,
                Cheek = cha.fileStatus.hohoAkaRate
            };
        }

        private static void TryReadFaceCtrlMinMax(ChaControl cha, string ctrlName, ref float min, ref float max)
        {
            if (cha == null || string.IsNullOrWhiteSpace(ctrlName))
            {
                return;
            }

            try
            {
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                Type chaType = cha.GetType();
                PropertyInfo ctrlProp = chaType.GetProperty(ctrlName, flags);
                FieldInfo ctrlField = chaType.GetField(ctrlName, flags);
                object ctrl = ctrlProp != null ? ctrlProp.GetValue(cha, null) : ctrlField?.GetValue(cha);
                if (ctrl == null)
                {
                    return;
                }

                Type ctrlType = ctrl.GetType();
                FieldInfo minField = ctrlType.GetField("OpenMin", BindingFlags.Instance | BindingFlags.Public);
                FieldInfo maxField = ctrlType.GetField("OpenMax", BindingFlags.Instance | BindingFlags.Public);
                if (minField != null)
                {
                    min = Convert.ToSingle(minField.GetValue(ctrl));
                }

                if (maxField != null)
                {
                    max = Convert.ToSingle(maxField.GetValue(ctrl));
                }
            }
            catch
            {
            }
        }

        private static bool AreFaceSnapshotsEqual(FaceSnapshot a, FaceSnapshot b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a == null || b == null)
            {
                return false;
            }

            return a.Eyebrow == b.Eyebrow
                && a.Eyes == b.Eyes
                && a.Mouth == b.Mouth
                && NearlyEqual(a.EyesOpenMin, b.EyesOpenMin)
                && NearlyEqual(a.EyesOpenMax, b.EyesOpenMax)
                && NearlyEqual(a.MouthOpenMin, b.MouthOpenMin)
                && NearlyEqual(a.MouthOpenMax, b.MouthOpenMax)
                && NearlyEqual(a.MouthFixedRate, b.MouthFixedRate)
                && a.Tears == b.Tears
                && NearlyEqual(a.Cheek, b.Cheek);
        }

        private static bool NearlyEqual(float a, float b)
        {
            if (float.IsNaN(a) && float.IsNaN(b))
            {
                return true;
            }

            return Mathf.Abs(a - b) <= 0.0005f;
        }

        private static string DescribeFaceSnapshotDiff(FaceSnapshot from, FaceSnapshot to)
        {
            if (from == null || to == null)
            {
                return "(snapshot-null)";
            }

            var parts = new List<string>(10);
            if (from.Eyebrow != to.Eyebrow) parts.Add("eyebrow:" + from.Eyebrow + "->" + to.Eyebrow);
            if (from.Eyes != to.Eyes) parts.Add("eyes:" + from.Eyes + "->" + to.Eyes);
            if (from.Mouth != to.Mouth) parts.Add("mouth:" + from.Mouth + "->" + to.Mouth);
            if (!NearlyEqual(from.EyesOpenMin, to.EyesOpenMin)) parts.Add("eyesMin:" + Round3(from.EyesOpenMin) + "->" + Round3(to.EyesOpenMin));
            if (!NearlyEqual(from.EyesOpenMax, to.EyesOpenMax)) parts.Add("eyesMax:" + Round3(from.EyesOpenMax) + "->" + Round3(to.EyesOpenMax));
            if (!NearlyEqual(from.MouthOpenMin, to.MouthOpenMin)) parts.Add("mouthMin:" + Round3(from.MouthOpenMin) + "->" + Round3(to.MouthOpenMin));
            if (!NearlyEqual(from.MouthOpenMax, to.MouthOpenMax)) parts.Add("mouthMax:" + Round3(from.MouthOpenMax) + "->" + Round3(to.MouthOpenMax));
            if (!NearlyEqual(from.MouthFixedRate, to.MouthFixedRate)) parts.Add("mouthFixed:" + Round3(from.MouthFixedRate) + "->" + Round3(to.MouthFixedRate));
            if (from.Tears != to.Tears) parts.Add("tears:" + from.Tears + "->" + to.Tears);
            if (!NearlyEqual(from.Cheek, to.Cheek)) parts.Add("cheek:" + Round3(from.Cheek) + "->" + Round3(to.Cheek));
            return parts.Count > 0 ? string.Join(", ", parts.ToArray()) : string.Empty;
        }

        private static string FormatFaceSnapshot(FaceSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return "(null)";
            }

            return "eyebrow=" + snapshot.Eyebrow
                + " eyes=" + snapshot.Eyes
                + " mouth=" + snapshot.Mouth
                + " eyesMin=" + Round3(snapshot.EyesOpenMin)
                + " eyesMax=" + Round3(snapshot.EyesOpenMax)
                + " mouthMin=" + Round3(snapshot.MouthOpenMin)
                + " mouthMax=" + Round3(snapshot.MouthOpenMax)
                + " mouthFixed=" + Round3(snapshot.MouthFixedRate)
                + " tears=" + snapshot.Tears
                + " cheek=" + Round3(snapshot.Cheek);
        }

        private static string Round3(float value)
        {
            if (float.IsNaN(value))
            {
                return "NaN";
            }

            return Math.Round(value, 3, MidpointRounding.AwayFromZero).ToString("0.###");
        }

        private static string ResolveFacePresetProbeCaller()
        {
            try
            {
                var trace = new System.Diagnostics.StackTrace(2, false);
                for (int i = 0; i < trace.FrameCount; i++)
                {
                    var method = trace.GetFrame(i)?.GetMethod();
                    if (method == null)
                    {
                        continue;
                    }

                    Type declaringType = method.DeclaringType;
                    string fullName = declaringType != null ? declaringType.FullName : string.Empty;
                    string asmName = declaringType != null && declaringType.Assembly != null
                        ? declaringType.Assembly.GetName().Name
                        : string.Empty;

                    if (string.Equals(asmName, "MainGameVoiceFaceEventBridge", StringComparison.Ordinal)
                        || string.Equals(asmName, "0Harmony", StringComparison.Ordinal)
                        || string.Equals(asmName, "HarmonyXInterop", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(fullName)
                        && fullName.IndexOf("Harmony", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        continue;
                    }

                    return (string.IsNullOrWhiteSpace(asmName) ? "(asm?)" : asmName)
                        + ":"
                        + (string.IsNullOrWhiteSpace(fullName) ? "(type?)" : fullName)
                        + "."
                        + method.Name;
                }
            }
            catch
            {
            }

            return "(unknown)";
        }

        private static void SetFaceCtrlMinMax(ChaControl cha, string ctrlName, float min, float max)
        {
            if (cha == null || string.IsNullOrWhiteSpace(ctrlName))
            {
                return;
            }

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type chaType = cha.GetType();
            PropertyInfo ctrlProp = chaType.GetProperty(ctrlName, flags);
            FieldInfo ctrlField = chaType.GetField(ctrlName, flags);
            object ctrl = ctrlProp != null ? ctrlProp.GetValue(cha, null) : ctrlField?.GetValue(cha);
            if (ctrl == null)
            {
                return;
            }

            Type ctrlType = ctrl.GetType();
            FieldInfo minField = ctrlType.GetField("OpenMin", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo maxField = ctrlType.GetField("OpenMax", BindingFlags.Instance | BindingFlags.Public);
            if (minField != null)
            {
                minField.SetValue(ctrl, min);
            }

            if (maxField != null)
            {
                maxField.SetValue(ctrl, max);
            }
        }

        private bool TryApplyFace(HSceneProc proc, int main, ChaControl female, int face, int voiceKind, int action)
        {
            if (face < 0)
            {
                return true;
            }

            object faceCtrl = GetFaceCtrlByFemaleIndex(proc, main);
            if (faceCtrl == null)
            {
                LogWarn("[cmd] faceCtrl not found main=" + main);
                return false;
            }

            try
            {
                var setFaceMethod = AccessTools.Method(faceCtrl.GetType(), "SetFace", new[] { typeof(int), typeof(ChaControl), typeof(int), typeof(int) });
                if (setFaceMethod == null)
                {
                    LogWarn("[cmd] SetFace method not found on " + faceCtrl.GetType().FullName);
                    return false;
                }

                object resultObj = setFaceMethod.Invoke(faceCtrl, new object[] { face, female, voiceKind, action });
                bool ok = resultObj is bool && (bool)resultObj;
                if (!ok)
                {
                    LogWarn("[cmd] SetFace failed face=" + face + " main=" + main);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                LogWarn("[cmd] SetFace exception: " + ex.Message);
                return false;
            }
        }

        private static object GetFaceCtrlByFemaleIndex(HSceneProc proc, int femaleIndex)
        {
            if (proc == null)
            {
                return null;
            }

            object face1 = Face1Field?.GetValue(proc);
            if (femaleIndex == 1 && face1 != null)
            {
                return face1;
            }

            return FaceField?.GetValue(proc);
        }

        private int ResolveFace(
            ExternalVoiceFaceCommand command,
            PluginSettings settings,
            out bool keepCurrentFaceMode)
        {
            keepCurrentFaceMode = command.ResolveKeepCurrentFace(settings.KeepCurrentFaceByDefault);
            if (keepCurrentFaceMode)
            {
                return -1;
            }

            int directFace = command.ResolveFace(-1);
            if (directFace >= 0)
            {
                return directFace;
            }

            int randomFromCommand = TrySelectRandomFace(command.faces);
            if (randomFromCommand >= 0)
            {
                return randomFromCommand;
            }

            if (settings.DefaultFace >= 0)
            {
                return settings.DefaultFace;
            }

            int randomFromSettings = TrySelectRandomFace(settings.RandomFaceCandidates);
            if (randomFromSettings >= 0)
            {
                return randomFromSettings;
            }

            return 0;
        }

        private int TrySelectRandomFace(int[] source)
        {
            if (source == null || source.Length == 0)
            {
                return -1;
            }

            int[] valid = new int[source.Length];
            int count = 0;
            for (int i = 0; i < source.Length; i++)
            {
                int face = source[i];
                if (face < 0)
                {
                    continue;
                }

                valid[count] = face;
                count++;
            }

            if (count <= 0)
            {
                return -1;
            }

            lock (_random)
            {
                return valid[_random.Next(count)];
            }
        }

        private HSceneProc FindCurrentProc()
        {
            if (CurrentProc != null)
            {
                return CurrentProc;
            }

            float now = Time.unscaledTime;
            if (now < _nextProcProbeTime)
            {
                return null;
            }

            _nextProcProbeTime = now + 1f;
            CurrentProc = UnityEngine.Object.FindObjectOfType<HSceneProc>();
            return CurrentProc;
        }

        private static int ClampMainIndex(HSceneProc proc, int index)
        {
            if (proc == null || proc.flags == null || proc.flags.lstHeroine == null || proc.flags.lstHeroine.Count <= 0)
            {
                return index < 0 ? 0 : index;
            }

            if (index < 0)
            {
                return 0;
            }

            if (index >= proc.flags.lstHeroine.Count)
            {
                return proc.flags.lstHeroine.Count - 1;
            }

            return index;
        }

        private static ChaControl ResolveFemale(HSceneProc proc, int main)
        {
            if (proc == null || LstFemaleField == null)
            {
                return null;
            }

            try
            {
                IList females = LstFemaleField.GetValue(proc) as IList;
                if (females == null || females.Count <= 0)
                {
                    return null;
                }

                int index = main;
                if (index < 0)
                {
                    index = 0;
                }
                if (index >= females.Count)
                {
                    index = females.Count - 1;
                }

                return females[index] as ChaControl;
            }
            catch (Exception ex)
            {
                LogWarn("resolve female failed: " + ex.Message);
                return null;
            }
        }

        private static int ResolveVoiceNo(HSceneProc proc, int main)
        {
            if (proc == null || proc.flags == null || proc.flags.lstHeroine == null)
            {
                return -1;
            }

            if (main < 0 || main >= proc.flags.lstHeroine.Count || proc.flags.lstHeroine[main] == null)
            {
                return -1;
            }

            return proc.flags.lstHeroine[main].voiceNo;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HSceneProc), "CreateListAnimationFileName")]
        private static void CreateListAnimationFileNamePostfix(HSceneProc __instance)
        {
            CurrentProc = __instance;
            if (Instance != null)
            {
                Instance.EnsurePoseClassificationFilesFromProc(__instance);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HSceneProc), "OnDestroy")]
        private static void HSceneOnDestroyPostfix(HSceneProc __instance)
        {
            if (CurrentProc == __instance)
            {
                if (Instance != null)
                {
                    Instance._blockGameVoiceUntil = 0f;
                    Instance.RestoreVoiceProcStopIfNeeded();
                    Instance._facePresetProbe = null;
                }
                CurrentProc = null;
                Log("released HSceneProc at OnDestroy");
            }
        }

        internal static void Log(string message)
        {
            if (Settings != null && !Settings.VerboseLog)
            {
                return;
            }
            Logger?.LogInfo(message);
            AppendFileLog(message);
        }

        internal static void LogAlways(string message)
        {
            Logger?.LogInfo(message);
            AppendFileLog(message);
        }

        internal static void LogWarn(string message)
        {
            Logger?.LogWarning(message);
            AppendFileLog("[WARN] " + message);
        }

        internal static void LogError(string message)
        {
            Logger?.LogError(message);
            AppendFileLog("[ERROR] " + message);
        }

        private static void AppendFileLog(string message)
        {
            if (string.IsNullOrEmpty(LogFilePath))
            {
                return;
            }

            try
            {
                lock (FileLogLock)
                {
                    File.AppendAllText(
                        LogFilePath,
                        $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}",
                        Utf8NoBom);
                }
            }
            catch
            {
            }
        }
    }
}
