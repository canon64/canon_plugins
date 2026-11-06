using System;
using CanonGate;
using HarmonyLib;
using UnityEngine;

namespace MainGameVMDPlay
{
    public sealed partial class Plugin
    {
        private bool _aiHFaceLease;
        public static bool IsAiHMotionRestored(long token) => Instance?._astral != null && Instance._astral.Token == token && Instance._astral.Restored;
        public static long SuspendAiHMotion(ChaControl actor)
        {
            var p = Instance;
            if (p == null || !RoomAllowed || !ReferenceEquals(p._trialChara, actor) || !p._vmdOwnsCharacter) return default(long);
            if (!ReferenceEquals(GateApi.AiShoujoMode?.Actor, actor) || p.IsIndependentTarget) throw new InvalidOperationException("専用HへのVMD譲渡対象が現在のAI住人と一致しません。");
            long token = SuspendForAstral();
            if (token == default(long)) throw new InvalidOperationException("VMDの一時保存を開始できません。");
            try { if (!ReleaseHForAstral(token)) throw new InvalidOperationException("VMDの身体操作権を返却できません。"); p._astral.TargetGeneration = p._target.Generation; return token; }
            catch { DiscardAstral(token); throw; }
        }
        public static Action AcquireAiHFaceLease(ChaControl actor)
        {
            var plugin = Instance;
            if (actor == null || !ReferenceEquals(GateApi.AiShoujoMode?.Actor, actor))
                throw new InvalidOperationException("AI専用Hの顔操作権は現在のAI住人だけに取得できます。");
            if (plugin == null || !RoomAllowed || !ReferenceEquals(plugin._trialChara, actor) || plugin._controller == null) return () => { };
            if (plugin._aiHFaceLease) throw new InvalidOperationException("VMDのAI顔操作権は既に借用されています。");
            var face = AccessTools.Field(plugin._controller.GetType(), "faceController")?.GetValue(plugin._controller) as Behaviour;
            if (face == null) return () => { };
            bool enabled = face.enabled; int generation = plugin._target.Generation, scene = plugin._target.SceneGeneration;
            plugin._aiHFaceLease = true; face.enabled = false;
            bool released = false;
            return () =>
            {
                if (released) return; released = true; plugin._aiHFaceLease = false;
                if (!ReferenceEquals(Instance, plugin) || !RoomAllowed || !ReferenceEquals(plugin._trialChara, actor)
                    || plugin._target.Generation != generation || plugin._target.SceneGeneration != scene || face == null) return;
                face.enabled = enabled;
            };
        }
    }
}
