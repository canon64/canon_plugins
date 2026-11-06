using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    public sealed partial class AiAnimationTrial
    {
        internal sealed class AdditionalResidentLease : IDisposable
        {
            internal ChaControl Actor;
            internal GameObject Root;
            public void Dispose()
            {
                var actor = Actor; Actor = null;
                try { if (actor != null) { actor.StopAllCoroutines(); Manager.Character.DeleteChara(actor); } }
                finally { if (Root != null) UnityEngine.Object.Destroy(Root); Root = null; }
            }
        }
        // Uses the normal AI-mode card/model/controller pipeline, borrowing its already loaded assets.
        // No replacement of the current resident and no ordinary-H character/session is created.
        internal AdditionalResidentLease LoadAdditionalResident(string residentId, string card, string coordinate, Vector3 position, Quaternion rotation)
        {
            if (!Guid.TryParseExact(residentId, "N", out _) || string.IsNullOrWhiteSpace(card) || _controller == null || _report == null)
                throw new InvalidOperationException("追加住人の安定ID・保存カード・AIモデル準備が不足しています。");
            var lease = new AdditionalResidentLease();
            try
            {
                var file = new ChaFileControl();
                if (!file.LoadCharaFile(card) || file.parameter.sex != P.I("math.one")) throw new InvalidDataException("追加住人の保存女性カードを読み込めません。");
                lease.Root = new GameObject("AI resident " + residentId); lease.Root.transform.SetParent(transform, false);
                lease.Root.transform.SetPositionAndRotation(position, rotation);
                int id = FindObjectsOfType<ChaControl>().Select(actor => actor.chaID).DefaultIfEmpty(P.I("math.zero")).Max() + P.I("math.one");
                lease.Actor = Manager.Character.CreateFemale(lease.Root, id, file, true);
                if (lease.Actor == null) throw new InvalidOperationException("追加AI住人の通常キャラ生成に失敗しました。");
                lease.Actor.Load();
                if (!string.IsNullOrWhiteSpace(coordinate))
                {
                    if (!lease.Actor.nowCoordinate.LoadFile(coordinate)) throw new InvalidDataException("追加住人の保存コーディネートを読めません。");
                    lease.Actor.Reload(noChangeClothes: false, noChangeHead: true, noChangeHair: true, noChangeBody: true);
                }
                if (!lease.Actor.loadEnd || lease.Actor.objAnim == null || lease.Actor.animBody == null) throw new InvalidOperationException("追加AI住人のモデル読込が未完了です。");
                var missing = _report.mapping.Where(pair => lease.Actor.objAnim.transform.Find(pair.path) == null).Select(pair => pair.target).ToArray();
                if (missing.Any()) throw new InvalidDataException("追加AI住人の対応骨が不足しています: " + string.Join(", ", missing));
                var animator = lease.Actor.animBody; animator.runtimeAnimatorController = _controller; animator.applyRootMotion = false; animator.fireEvents = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind(); lease.Actor.SetActiveTop(true); lease.Actor.visibleAll = true;
                return lease;
            }
            catch { lease.Dispose(); throw; }
        }
    }
}
