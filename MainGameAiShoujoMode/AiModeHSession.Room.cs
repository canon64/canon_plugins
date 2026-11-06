using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    public sealed partial class AiModeHSession
    {
        internal bool RoomReady { get; private set; }
        internal bool AllRoomSelections { get; set; }
        internal event Action RoomSelectionChanged, RoomPositionReset, RoomEnding;
        internal event Action RoomBeforeEnding;
        internal Action<HSceneProc> PrepareRoomPoseCatalog;
        private static bool BeforeCategories(HSceneProc __instance)
        {
            var owner = _active;
            if (owner == null || owner._proc != __instance) return true;
            var use = (List<int>)AccessTools.Field(typeof(HSceneProc), "useCategorys").GetValue(__instance);
            use.Clear(); use.AddRange(owner._location.Categories);
            // CreateAllAnimationList has not run here. Preserve the supplied initial
            // category; expand availability only after the native catalog is built.
            var nearby = (IList)AccessTools.Field(typeof(HSceneProc), "closeHpointData").GetValue(__instance);
            nearby.Clear(); nearby.Add(owner._point);
            AccessTools.Field(typeof(HSceneProc), "closeInitPoint").SetValue(__instance, false);
            AccessTools.Field(typeof(HSceneProc), "nowInitPoint").SetValue(__instance, false);
            AccessTools.Field(typeof(HSceneProc), "useInitPosPoint").SetValue(__instance, false);
            return false;
        }
        private static void AfterRoomCatalog(HSceneProc __instance)
        {
            var owner = _active;
            if (owner?._proc != __instance || !owner.AllRoomSelections) return;
            owner.TraceRoomPoseCatalog("hotel-rebuild.before");
            var all = (List<HSceneProc.AnimationListInfo>[])AccessTools.Field(typeof(HSceneProc), "lstAnimInfo").GetValue(__instance);
            owner.TraceRoomPoseCatalog("native-source.generated", all);
            var categories = all.Where(list => list != null).SelectMany(list => list).Where(info => info != null)
                .SelectMany(info => info.lstCategory).Select(category => category.category).Distinct().ToArray();
            var use = (List<int>)AccessTools.Field(typeof(HSceneProc), "useCategorys").GetValue(__instance);
            use.Clear(); use.AddRange(categories);
            owner._point.category = categories;
            // categorys remains the actual entry category. Replacing it with all
            // categories selects special-scene startup paths instead of this room.
            AccessTools.Field(typeof(HSceneProc), "lstUseAnimInfo").SetValue(__instance,
                all.Select(list => list == null ? new List<HSceneProc.AnimationListInfo>() : new List<HSceneProc.AnimationListInfo>(list)).ToArray());
            owner.TraceRoomPoseCatalog("hotel-rebuild.after");
        }
        private static void AfterRoomSelection(HSceneProc __instance)
        {
            var owner = _active;
            if (owner?._proc == __instance && owner.RoomReady) owner.RoomSelectionChanged?.Invoke();
        }
        private static void AfterRoomPositionReset(HSceneProc __instance)
        {
            var owner = _active;
            if (owner?._proc == __instance && owner.RoomReady) owner.RoomPositionReset?.Invoke();
        }
        internal HotelRoomSelection ReadRoomSelection()
        {
            var info = _proc?.flags?.nowAnimationInfo;
            return Describe(info);
        }
        private static HotelRoomSelection Describe(HSceneProc.AnimationListInfo info)
        {
            return info == null ? null : new HotelRoomSelection { mode = info.mode.ToString(), id = info.id, name = info.nameAnimation,
                bundle = info.paramFemale?.path?.assetpath, asset = info.paramFemale?.path?.file };
        }
        internal bool RestoreRoomSelection(HotelRoomSelection saved)
            => RestoreRoomSelection(saved, out _);
        internal bool RestoreRoomSelection(HotelRoomSelection saved, out string reason)
        {
            reason = null;
            if (!RoomReady || saved == null) { reason = "客室が未準備、または保存体位がありません。"; return false; }
            PrepareRoomPoseCatalog?.Invoke(_proc);
            TraceRoomPoseCatalog("restore-attempt");
            var catalog = (List<HSceneProc.AnimationListInfo>[])AccessTools.Field(typeof(HSceneProc), "lstUseAnimInfo").GetValue(_proc);
            var matches = catalog.Where(list => list != null).SelectMany(list => list).Where(info => saved.Matches(Describe(info))).Distinct().ToArray();
            if (matches.Length != _p.I("math.one")) { TraceRoomPoseCatalog("restore-mismatch", details: true); reason = "完全一致の体位候補数=" + matches.Length + "（mode/id/name/bundle/asset）。初期体位を維持しました。"; return false; }
            AccessTools.Method(typeof(HSceneProc), "ChangeAnimator").Invoke(_proc, new object[] { matches.Single(), false });
            bool applied = saved.Matches(ReadRoomSelection());
            if (!applied) reason = "一致候補数=1ですが、ChangeAnimator後の実体位が保存体位と一致しません。";
            return applied;
        }
        private Transform[] RoomMembers()
        {
            if (!RoomReady || _proc == null) throw new InvalidOperationException("客室のキャラを読み込み中です。");
            var females = (List<ChaControl>)AccessTools.Field(typeof(HSceneProc), "lstFemale").GetValue(_proc);
            if (females == null || !females.Any(c => c != null)) throw new InvalidOperationException("女の子が見つかりません。");
            return females.Concat(new[] { "male", "male1" }.Select(name => AccessTools.Field(typeof(HSceneProc), name).GetValue(_proc) as ChaControl))
                .Where(c => c != null).Select(c => c.transform).Distinct().ToArray();
        }
        internal ChaControl[] RoomActors() => RoomMembers().Select(t => t.GetComponent<ChaControl>()).Where(a => a != null).ToArray();
        internal HFlag RoomFlags => _proc?.flags;
        internal HotelRoomPlacement ReadRoomPlacement(Transform room)
        {
            var female = RoomMembers().First(); var point = room.InverseTransformPoint(female.position);
            return new HotelRoomPlacement { x = point.x, y = point.y, z = point.z, yaw = (Quaternion.Inverse(room.rotation) * female.rotation).eulerAngles.y };
        }
        internal void ApplyRoomPlacement(Transform room, HotelRoomPlacement value)
        {
            value.Validate(); var members = RoomMembers(); var female = members.First();
            var origin = female.position;
            var target = room.TransformPoint(new Vector3(value.x, value.y, value.z));
            var delta = room.rotation * Quaternion.Euler(_p.N("math.zero"), value.yaw, _p.N("math.zero")) * Quaternion.Inverse(female.rotation);
            foreach (var member in members) member.SetPositionAndRotation(target + delta * (member.position - origin), delta * member.rotation);
            var actor = female.GetComponent<ChaControl>();
            if (actor?.objTop != null) _proc.flags.ctrlCamera?.SetWorldBase(actor.objTop.transform);
        }
    }
}
