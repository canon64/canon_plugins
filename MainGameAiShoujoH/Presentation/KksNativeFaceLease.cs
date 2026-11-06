using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace MainGameAiShoujoH.Presentation
{
    // KKS face-list IDs are resolved by the native KKS table. No AI face mesh,
    // profile, index conversion, PtnSet extension, or skeleton is involved.
    internal sealed class KksNativeFaceLease : IDisposable
    {
        readonly ChaControl actor;
        readonly List<Action> restore = new List<Action>();
        readonly GameObject root;
        readonly FaceListCtrl table;
        readonly Dictionary<string, Manager.Game.Expression> expressions;
        bool disposed;

        internal KksNativeFaceLease(ChaControl actor)
        {
            this.actor = actor ?? throw new ArgumentNullException(nameof(actor));
            int eyes = actor.fileStatus.eyesPtn, brows = actor.fileStatus.eyebrowPtn, mouth = actor.fileStatus.mouthPtn;
            bool blink = actor.fileStatus.eyesBlink, highlight = actor.fileStatus.hideEyesHighlight, shaking = actor.fileStatus.eyesYure, mask = actor.fileStatus.disableMouthShapeMask;
            float cheek = actor.fileStatus.hohoAkaRate; byte tears = actor.tearsLv;
            restore.Add(() => { if (actor == null) return; actor.ChangeEyesPtn(eyes); actor.ChangeEyebrowPtn(brows); actor.ChangeMouthPtn(mouth); actor.ChangeEyesBlinkFlag(blink); actor.HideEyeHighlight(highlight); actor.ChangeEyesShaking(shaking); actor.DisableShapeMouth(mask); actor.ChangeHohoAkaRate(cheek); actor.tearsLv = tears; });
            var face = actor.objHead == null ? null : actor.objHead.GetComponent<FaceBlendShape>();
            if (face == null) throw new InvalidDataException("KKS native face controller is missing.");
            foreach (FBSBase control in new FBSBase[] { face.EyebrowCtrl, face.EyesCtrl, face.MouthCtrl })
            {
                if (control == null) throw new InvalidDataException("KKS native FBS controller is missing.");
                var original = control; float min = control.OpenMin, max = control.OpenMax, rate = control.FixedRate;
                restore.Add(() => { original.OpenMin = min; original.OpenMax = max; original.FixedRate = rate; });
            }
            try
            {
                root = new GameObject("KKS H native face " + actor.GetInstanceID());
                var component = root.AddComponent(typeof(FaceListCtrl));
                ((Behaviour)component).enabled = false;
                table = (FaceListCtrl)(object)component;
                var initialize = typeof(FaceListCtrl).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new MissingMethodException("FaceListCtrl.Start");
                initialize.Invoke(table, null);
                if (!table.LoadTextD("c00")) throw new InvalidDataException("KKS sexy c00 native face table could not be loaded.");
                expressions = Manager.Game.ExpCharaListLoadData(default(int));
                if (expressions == null || expressions.Count == default(int)) throw new InvalidDataException("KKS c00 native ADV expression table is unavailable.");
            }
            catch { Dispose(); throw; }
        }

        internal void Apply(int faceId, int voiceKind, int action)
        {
            if (disposed || actor == null) throw new ObjectDisposedException(nameof(KksNativeFaceLease));
            if (!table.SetFace(faceId, actor, voiceKind, action)) throw new InvalidDataException("KKS native face key is unavailable: " + voiceKind + "/" + action + "/" + faceId);
            var face = table.facelib[voiceKind][action][faceId];
            // Same event-only endpoint policy used by the existing AI-mode KKS
            // FaceList bridge. Native FreeH's continuous OpenCtrl loop is not
            // duplicated here; its 0.3-second transition is not claimed as ported.
            actor.ChangeEyesOpenMax(face.openMaxEye);
            if (actor.mouthCtrl != null) actor.mouthCtrl.OpenMin = face.openMinMouth;
        }
        internal void ApplyExpression(string nativeAdvExpression)
        {
            if (disposed || actor == null) throw new ObjectDisposedException(nameof(KksNativeFaceLease));
            var expression = Manager.Game.GetExpression(expressions, nativeAdvExpression);
            if (expression == null) throw new InvalidDataException("KKS c00 native ADV expression is unavailable: " + nativeAdvExpression);
            expression.Change(actor);
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            var errors = new List<Exception>();
            foreach (var action in Enumerable.Reverse(restore)) try { action(); } catch (Exception error) { errors.Add(error); }
            restore.Clear();
            if (root != null) UnityEngine.Object.Destroy(root);
            if (errors.Count != 0) throw new AggregateException("KKS native face restoration failed.", errors);
        }
    }
}
