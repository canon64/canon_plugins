using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MainGameAiShoujoH.Motion
{
    // Native HScene.ADVParam.Init uses this exact ADV controller/IK pack.
    // Preparation loads data only; actor ownership changes at Begin after H ends.
    internal sealed class NativeKksEndingPlayback : IDisposable
    {
        internal const string DefaultState = "Idle_00_00";
        readonly ChaControl actor;
        readonly Animator animator;
        readonly RuntimeAnimatorController controller;
        readonly MotionIKData data;
        RuntimeAnimatorController previousController;
        bool previousEvents, begun, disposed;
        float previousSpeed;
        MotionIK ik;
        internal string State { get; private set; }
        internal NativeKksEndingPlayback(ChaControl actor)
        {
            this.actor = actor ?? throw new ArgumentNullException(nameof(actor));
            animator = actor.animBody ?? throw new InvalidDataException("KKS ending actor Animator is missing");
            controller = CommonLib.LoadAsset<RuntimeAnimatorController>("adv/motion/controller/adv/00.unity3d", "cf_adv_00_00");
            var asset = CommonLib.LoadAsset<TextAsset>("adv/motion/iklist/00.unity3d", "cf_adv_00_00");
            if (controller == null || asset == null) throw new InvalidDataException("Installed KKS native ADV controller/IK pack is missing: cf_adv_00_00");
            data = new MotionIKData(asset);
        }
        internal void Begin()
        {
            if (disposed || begun || actor.animBody != animator) throw new InvalidOperationException("KKS ending body ownership differs");
            previousController = animator.runtimeAnimatorController; previousEvents = animator.fireEvents; previousSpeed = animator.speed;
            begun = true;
            animator.runtimeAnimatorController = controller; animator.fireEvents = false; animator.speed = 1;
            // Same per-session native IK data copy as Game.AnimePack.SetDefalut.
            ik = new MotionIK(actor, data.Copy());
            Play(DefaultState);
        }
        internal void Play(string exactNativeState)
        {
            if (disposed || !begun || actor.animBody != animator || animator.runtimeAnimatorController != controller) throw new InvalidOperationException("KKS ending body lease is not active");
            if (string.IsNullOrWhiteSpace(exactNativeState) || !animator.HasState(0, Animator.StringToHash(exactNativeState))) throw new InvalidDataException("KKS native ADV state is missing: " + exactNativeState);
            // EasyLoader.IKMotion.Setting disables unavailable IK states; it does
            // not map them to an unrelated H pose or synthesize contacts.
            ik.enabled = ik.GetNowState(exactNativeState) != null;
            if (ik.enabled) ik.Calc(exactNativeState); else ik.Reset();
            animator.Play(exactNativeState, 0, 0); animator.Update(0); State = exactNativeState;
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            var failures = new List<Exception>();
            void Cleanup(Action action)
            {
                try { action(); }
                catch (Exception error) { failures.Add(error); }
            }
            if (begun)
            {
                Cleanup(() => ik?.Reset());
                Cleanup(() => ik?.Release());
                Cleanup(() =>
                {
                    if (actor != null && animator != null && actor.animBody == animator)
                        animator.runtimeAnimatorController = previousController;
                });
                Cleanup(() =>
                {
                    if (actor != null && animator != null && actor.animBody == animator)
                        animator.fireEvents = previousEvents;
                });
                Cleanup(() =>
                {
                    if (actor != null && animator != null && actor.animBody == animator)
                        animator.speed = previousSpeed;
                });
            }
            Cleanup(() => data.Release());
            if (failures.Count != 0) throw new AggregateException("KKS ending body cleanup failed", failures);
        }
    }
}
