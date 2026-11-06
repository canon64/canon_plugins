using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using H;
using MainGameAiShoujoH.Contracts;
using UnityEngine;

namespace MainGameAiShoujoH.Integration
{
    internal sealed partial class NativeKksOwnedScene
    {
        HPointData currentPoint;
        Presentation.HPointSelectionPanel pointPanel;
        bool movingPoint;
        string pointFailureId;

        void ReportPointFailure(string message)
        {
            context.ReportFailure(message);
            pointFailureId = MainGameNotifications.NotificationApi.Publish(Plugin.PluginGuid,
                "Hポイントの操作失敗", message, MainGameNotifications.NotificationSeverity.Failure, pointFailureId);
        }

        IEnumerable<HSceneProc.AnimationListInfo> NativeChoices()
            => ((List<HSceneProc.AnimationListInfo>[])AccessTools.Field(typeof(HSceneProc), "lstUseAnimInfo").GetValue(proc))
                .Where(list => list != null).SelectMany(list => list);

        static bool BeforeCloseCategories(HSceneProc __instance)
        {
            var owner = active;
            if (owner?.Owns(__instance) != true) return true;
            if (owner.currentPoint == null)
                owner.currentPoint = owner.CreatePoint(new HPointChoice { Id = "entry", Position = owner.context.Request.Position,
                    Rotation = owner.context.Request.Rotation, Categories = owner.runtime.SelectedAnimation.lstCategory.Select(c => c.category).Distinct().ToArray() });
            owner.SetPointCategories();
            return false;
        }
        void SetPointCategories()
        {
            var use = (List<int>)AccessTools.Field(typeof(HSceneProc), "useCategorys").GetValue(proc);
            use.Clear(); use.AddRange(currentPoint.category);
            var close = (List<HPointData>)AccessTools.Field(typeof(HSceneProc), "closeHpointData").GetValue(proc);
            close.Clear(); close.Add(currentPoint);
            foreach (var field in new[] { "closeInitPoint", "nowInitPoint", "useInitPosPoint" })
                AccessTools.Field(typeof(HSceneProc), field).SetValue(proc, false);
        }
        // Our list never hides actors or changes native camera DOF, so native
        // ChangeCategory must not run HPointMove's unrelated visibility restore.
        static bool BeforeReturnPointMove(HSceneProc __instance) => active?.Owns(__instance) != true;

        HPointData CreatePoint(HPointChoice choice)
        {
            var root = new GameObject("AI H point " + choice.Id);
            root.transform.SetParent(context.Request.Island.transform, true);
            root.transform.SetPositionAndRotation(choice.Position, choice.Rotation);
            var point = root.AddComponent<HPointData>();
            point.category = choice.Categories.ToArray();
            AccessTools.Field(typeof(HPointData), "_targets").SetValue(point, Array.Empty<string>());
            AccessTools.Field(typeof(HPointData), "_groups").SetValue(point, Array.Empty<string>());
            AccessTools.Field(typeof(HPointData), "_experience").SetValue(point, default(int));
            point.SetKindOffset(Vector3.zero, Vector3.zero); point.BackUpPosition();
            return point;
        }
        HPointChoice[] ReadPoints()
        {
            var origin = currentPoint != null ? currentPoint.transform.position : context.Request.Position;
            var all = (List<HSceneProc.AnimationListInfo>[])AccessTools.Field(typeof(HSceneProc), "lstAnimInfo").GetValue(proc);
            var unlocked = Manager.Game.globalData.playHList;
            var categories = new HashSet<int>(all.Where(list => list != null).SelectMany(list => list)
                .Where(row => row.mode >= HFlag.EMode.aibu && row.mode <= HFlag.EMode.sonyu)
                .Where(row => unlocked.TryGetValue((int)row.mode, out var ids) && ids.Contains(row.id)
                    && row.stateRestriction <= (int)proc.flags.lstHeroine[0].HExperience)
                .SelectMany(row => row.lstCategory).Select(c => c.category));
            return (context.Request.NearbyHPoints?.Invoke(origin) ?? Array.Empty<HPointChoice>())
                .Where(point => point != null && !string.IsNullOrEmpty(point.Id) && point.Categories?.Any(categories.Contains) == true
                    && HPointRange.Contains(origin, point.Position)).ToArray();
        }
        void OpenPoints()
        {
            if (movingPoint) return;
            try
            {
                var points = ReadPoints();
                if (pointPanel == null) pointPanel = new GameObject("AI H nearby points").AddComponent<Presentation.HPointSelectionPanel>();
                var origin = currentPoint.transform.position;
                pointPanel.Open(points, origin, () => Current && IsPrepared && !movingPoint && context.CanInteract(), MovePoint,
                    ReportPointFailure);
            }
            catch (Exception error) { ReportPointFailure("Hポイント一覧: " + error.Message); }
        }
        void MovePoint(string id)
        {
            if (!Current || !IsPrepared || movingPoint || !context.CanInteract()) return;
            // Validate again against the current origin; callers cannot supply an
            // arbitrary destination or reuse an out-of-range list from an old point.
            var choice = ReadPoints().SingleOrDefault(point => point.Id == id)
                ?? throw new InvalidOperationException("現在地から10m以内のHポイントではありません。");
            movingPoint = true; pointPanel?.Release(); SuspendSelection();
            try { context.Request.Cover(() => { if (Current) context.Request.Owner.StartCoroutine(ApplyPoint(choice)); }); }
            catch { movingPoint = false; ResumeSelection(); throw; }
        }
        IEnumerator ApplyPoint(HPointChoice choice)
        {
            yield return new WaitForEndOfFrame();
            if (!Current || !IsPrepared) yield break;
            var previous = currentPoint;
            var previousAnimation = proc.flags.nowAnimationInfo;
            HPointData next = null;
            try
            {
                if (!HPointRange.Contains(previous.transform.position, choice.Position)) throw new InvalidOperationException("移動先が10mの範囲外です。");
                next = CreatePoint(choice); currentPoint = next; SetPointCategories();
                nativeCall = true;
                var all = (List<HSceneProc.AnimationListInfo>[])AccessTools.Field(typeof(HSceneProc), "lstAnimInfo").GetValue(proc);
                var unlocked = Manager.Game.globalData.playHList;
                var category = next.category.First(c => all.Where(list => list != null).SelectMany(list => list)
                    .Any(row => row.mode >= HFlag.EMode.aibu && row.mode <= HFlag.EMode.sonyu
                        && unlocked.TryGetValue((int)row.mode, out var ids) && ids.Contains(row.id)
                        && row.stateRestriction <= (int)proc.flags.lstHeroine[0].HExperience && row.lstCategory.Any(item => item.category == c)));
                proc.ChangeCategory(next, category);
                if (!NativeChoices().Contains(proc.flags.nowAnimationInfo)) throw new InvalidOperationException("この地点に適用できる体位がありません。");
                UnityEngine.Object.Destroy(previous.gameObject);
            }
            catch (Exception error)
            {
                currentPoint = previous; SetPointCategories();
                try {
                    proc.ChangeCategory(previous, previous.category.First());
                    if (previousAnimation != null && NativeChoices().Contains(previousAnimation))
                        AccessTools.Method(typeof(HSceneProc), "ChangeAnimator").Invoke(proc, new object[] { previousAnimation, false });
                }
                catch (Exception rollback) { ReportPointFailure("Hポイントの位置復帰: " + rollback.Message); }
                if (next != null) UnityEngine.Object.Destroy(next.gameObject);
                ReportPointFailure("Hポイント移動: " + error.Message);
            }
            finally { nativeCall = false; movingPoint = false; }
            if (Current) context.Request.Reveal(() => { if (Current && IsPrepared) { ResumeSelection(); OpenPoints(); } });
        }
        void DisposePoints()
        {
            movingPoint = false;
            if (pointPanel != null) { pointPanel.Release(); UnityEngine.Object.Destroy(pointPanel.gameObject); pointPanel = null; }
            if (currentPoint != null) { UnityEngine.Object.Destroy(currentPoint.gameObject); currentPoint = null; }
        }
    }
}
