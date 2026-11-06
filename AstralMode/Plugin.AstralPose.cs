using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;

namespace AstralMode
{
    // アストラル入場時に本体側の女をベンチフェラ待機へ強制切替する。
    // 銅像はこの前に現在体位のまま焼く。ここでは銅像ではなく本体側だけを切り替える。
    public sealed partial class Plugin
    {
        private const string DefaultAstralPoseKeyword = "ベンチフェラ";

        private ConfigEntry<string> _cfgAstralPoseKeyword;
        private ConfigEntry<string> _cfgAstralPoseMode;

        private string AstralPoseKeyword
        {
            get
            {
                if (_cfgAstralPoseKeyword == null)
                    _cfgAstralPoseKeyword = Config.Bind(
                        "Astral State", "AstralPoseNameKeyword", DefaultAstralPoseKeyword,
                        "アストラル入場後、本体側の女に強制する体位の nameAnimation キーワード（部分一致）。空ならベンチフェラ。ASTRAL_POSE_LISTログで実名を確認できる。");

                string value = (_cfgAstralPoseKeyword.Value ?? string.Empty).Trim();
                return value.Length > 0 ? value : DefaultAstralPoseKeyword;
            }
        }

        private string AstralPoseModeName
        {
            get
            {
                if (_cfgAstralPoseMode == null)
                    _cfgAstralPoseMode = Config.Bind(
                        "Astral State", "AstralPoseMode", "houshi",
                        "アストラル入場後、本体側の女に強制する体位のモード（EMode名の部分一致: houshi/sonyu/aibu 等）。空なら全モード対象。");
                return _cfgAstralPoseMode.Value;
            }
        }

        private static readonly FieldInfo AstralAllPosesField = AccessTools.Field(typeof(HSceneProc), "lstAnimInfo");
        private static readonly MethodInfo AstralChangeAnimator = AccessTools.Method(typeof(HSceneProc), "ChangeAnimator",
            new[] { typeof(HSceneProc.AnimationListInfo), typeof(bool) });

        internal bool ForceAstralPose()
        {
            try
            {
                HSceneProc proc = _hSceneProc;
                if (proc == null || proc.flags == null)
                {
                    return AstralPoseFailed("HSceneProc/flags がありません。");
                }

                var lists = SnapLstUseAnimInfoField != null
                    ? SnapLstUseAnimInfoField.GetValue(proc) as List<HSceneProc.AnimationListInfo>[]
                    : null;
                string keyword = (AstralPoseKeyword ?? DefaultAstralPoseKeyword).Trim();
                if (keyword.Length == 0) keyword = DefaultAstralPoseKeyword;
                string modeName = (AstralPoseModeName ?? string.Empty).Trim().ToLowerInvariant();

                // The real H point can exclude bench poses. Astral entry owns its scenery.
                var all = AstralAllPosesField?.GetValue(proc) as List<HSceneProc.AnimationListInfo>[];
                var best = FindAstralPose(lists, keyword, modeName) ?? FindAstralPose(all, keyword, modeName);
                if (best == null)
                    return AstralPoseFailed($"開始体位が見つかりません: {keyword} / {modeName}");
                if (AstralChangeAnimator == null)
                    return AstralPoseFailed("HSceneProc.ChangeAnimator が見つかりません。");

                // A queued selection waits for voiceWait in HSceneProc.Update. During
                // the warp voices are stopped, so apply the native change here instead.
                proc.flags.voiceWait = false;
                proc.flags.click = HFlag.ClickKind.actionChange;
                AstralChangeAnimator.Invoke(proc, new object[] { best, false });
                if (!ReferenceEquals(proc.flags.nowAnimationInfo, best))
                    return AstralPoseFailed($"開始体位を適用できませんでした: {best.nameAnimation}");
                proc.flags.selectAnimationListInfo = null;
                proc.flags.click = HFlag.ClickKind.none;
                if (DiagnosticsEnabled)
                    SimpleFileLogger.Info($"ForceAstralPose: applied id={best.id} mode={best.mode} name={best.nameAnimation}");
                return true;
            }
            catch (Exception ex)
            {
                return AstralPoseFailed((ex.InnerException ?? ex).Message);
            }
        }

        private bool AstralPoseFailed(string reason)
        {
            if (DiagnosticsEnabled) SimpleFileLogger.Warn("ForceAstralPose failed: " + reason);
            MainGameNotifications.NotificationApi.Publish(Guid, "アストラル開始失敗",
                "開始体位の切替に失敗したため現実へ戻ります。" + reason,
                MainGameNotifications.NotificationSeverity.Failure);
            return false;
        }

        private static HSceneProc.AnimationListInfo FindAstralPose(
            List<HSceneProc.AnimationListInfo>[] lists, string keyword, string modeName)
        {
            HSceneProc.AnimationListInfo partial = null;
            if (lists == null) return null;
            for (int i = 0; i < lists.Length; i++)
            {
                var list = lists[i];
                if (list == null) continue;

                for (int j = 0; j < list.Count; j++)
                {
                    var c = list[j];
                    if (c == null) continue;
                    string mode = c.mode.ToString().ToLowerInvariant();
                    bool modeOk = modeName.Length == 0 || mode.IndexOf(modeName) >= 0;
                    if (!modeOk) continue;

                    string name = c.nameAnimation ?? string.Empty;
                    if (string.Equals(name, keyword, StringComparison.OrdinalIgnoreCase))
                        return c;

                    if (partial == null && name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                        partial = c;
                }
            }
            return partial;
        }
    }
}
