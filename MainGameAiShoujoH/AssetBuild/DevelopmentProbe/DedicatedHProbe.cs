using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System.Reflection;
using CanonGate;
using MainGameAiShoujoMode;
using MainGameDevelopmentBridge;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using HPlugin = MainGameAiShoujoH.Plugin;

public sealed class DedicatedHProbe : IDevelopmentCommand
{
    public string Name => "ai-dedicated-h";
    public object Execute(Dictionary<string, object> arguments, DevelopmentContext context)
    {
        var owner = GateApi.AiShoujoMode?.Owner as AiModeSession;
        string action = ReflectionCommands.Text(arguments, "action", "status");
        if (action != "status" && owner == null) throw new InvalidOperationException("The published AI mode owner is not ready.");
        if (action == "start") owner.StartDedicatedHWithResidents(ReflectionCommands.Text(arguments, "motion", "0:0"), ReflectionCommands.Text(arguments, "residents", string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), arguments.TryGetValue("event", out var eventId) ? Convert.ToInt32(eventId) : default(int), arguments.TryGetValue("height", out var height) ? Convert.ToInt32(height) : default(int));
        else if (action == "start-kks-one-pose") HPlugin.RunKksOnePoseTrial(owner, () => owner.StartDedicatedHWithMotion("houshi:2", default(int), default(int)));
        else if (action == "cancel") owner.EndDedicatedH(false);
        else if (action == "complete") owner.EndDedicatedH(true);
        else if (action == "command") owner.CommandDedicatedH(ReflectionCommands.Text(arguments, "name"), ReflectionCommands.Text(arguments, "value"), arguments.TryGetValue("amount", out var amount) ? Convert.ToSingle(amount) : 0);
        else if (action != "status") throw new ArgumentException("Unknown dedicated-H development action: " + action);
        var state = HPlugin.ReadState();
        return new {
            ownerReady = owner != null, frame = Time.frameCount, environment = owner?.ReadDedicatedHEnvironment(), playback = ReadPlayback(),
            session = state == null ? null : new { id = state.Id.ToString("N"), state.Generation, phase = state.Phase.ToString(), state.Ready, state.MotionId, state.MotionState,
                state.Participants, state.ResultPendingCount, state.ResultAppliedCount, state.Restored, state.EndingScenario, state.EndingReceiveSaved, state.EndingCompleted, endReason = state.LastEndReason?.ToString(), state.Failures },
            actors = Resources.FindObjectsOfTypeAll<ChaControl>().Where(a => a != null && a.gameObject.scene.IsValid() && a.gameObject.scene.isLoaded).Select(a => new { id = a.GetInstanceID(), a.name, a.sex, active = a.gameObject.activeInHierarchy, visible = a.visibleAll }).ToArray(),
            ownedObjects = Resources.FindObjectsOfTypeAll<GameObject>().Where(o => o != null && o.scene.IsValid() && o.scene.isLoaded && (o.name.StartsWith("AI H ", StringComparison.Ordinal) || o.name.StartsWith("AI resident ", StringComparison.Ordinal) || o.name.StartsWith("KKS ", StringComparison.Ordinal) || o.scene.name == "H" || o.scene.name == "HProc")).Select(o => new { id = o.GetInstanceID(), o.name, scene = o.scene.name, active = o.activeInHierarchy }).ToArray()
        };
    }
    private static object Field(object owner, string name) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    private static object[] ReadPlayback()
    {
        var plugin = typeof(HPlugin).GetField("Instance", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        if (plugin == null) return Array.Empty<object>();
        var session = Field(plugin, "_session");
        var modules = ((IEnumerable)Field(session, "_owned")).Cast<object>();
        var motion = modules.SingleOrDefault(module => module is MainGameAiShoujoH.Contracts.IMotionModule);
        if (motion == null) return Array.Empty<object>();
        var selected = ((MainGameAiShoujoH.Contracts.IMotionModule)motion).SelectedState;
        if (motion.GetType().FullName == "MainGameAiShoujoH.Integration.NativeKksOwnedScene")
        {
            var nativeProc = (HSceneProc)Field(motion, "proc");
            var scope = (MainGameAiShoujoH.Contracts.SessionContext)Field(motion, "context");
            if (nativeProc == null || scope == null) return Array.Empty<object>();
            return scope.Request.Participants.Select(participant => {
                var actor = participant.Character; var animator = actor.animBody; var state = animator.GetCurrentAnimatorStateInfo(0); var audio = actor.asVoice;
                return (object)new { backend = "kks-owned-HProc", role = participant.Role, actor = actor.GetInstanceID(), selected,
                    nativeProc = nativeProc.GetInstanceID(), nativeProc.enabled, gateOwned = GateApi.IsDedicatedNativeEngine(nativeProc),
                    flags = nativeProc.flags.GetInstanceID(), nativeMode = nativeProc.flags.mode.ToString(), nativePose = nativeProc.flags.nowAnimationInfo.id,
                    nativeProc.flags.gaugeFemale, nativeProc.flags.gaugeMale, nativeProc.flags.speedCalc, nativeProc.flags.voiceWait,
                    expectedHash = Animator.StringToHash(selected ?? string.Empty), stateHash = state.shortNameHash, normalizedTime = state.normalizedTime,
                    length = state.length, speed = animator.speed, controller = animator.runtimeAnimatorController == null ? null : animator.runtimeAnimatorController.name,
                    clips = animator.GetCurrentAnimatorClipInfo(0).Select(clip => new { clip.clip.name, id = clip.clip.GetInstanceID(), length = clip.clip.length, clip.weight }).ToArray(),
                    audioPlaying = audio != null && audio.isPlaying, audioClip = audio == null || audio.clip == null ? null : audio.clip.name,
                    handEnabled = nativeProc.hand != null && nativeProc.hand.enabled,
                    camera = nativeProc.flags.ctrlCamera.thisCmaera.GetInstanceID(), cameraEnabled = nativeProc.flags.ctrlCamera.enabled
                };
            }).ToArray();
        }
        var kks = Field(motion, "_kksOnePose");
        if (kks != null)
        {
            var actors = (IDictionary)Field(kks, "animators");
            return actors.Keys.Cast<string>().Select(role => {
                var animator = (Animator)actors[role]; var state = animator.GetCurrentAnimatorStateInfo(0);
                return (object)new { backend = "kks-one-pose", role, selected, expectedHash = Animator.StringToHash(selected), stateHash = state.shortNameHash,
                    normalizedTime = state.normalizedTime, length = state.length, speed = animator.speed,
                    controller = animator.runtimeAnimatorController == null ? null : animator.runtimeAnimatorController.name,
                    clips = animator.GetCurrentAnimatorClipInfo(0).Select(clip => new { clip.clip.name, id = clip.clip.GetInstanceID(), length = clip.clip.length, clip.weight }).ToArray() };
            }).ToArray();
        }
        var native = Field(motion, "_native");
        if (native == null) return Array.Empty<object>();
        var roles = ((IEnumerable)Field(native, "_roles")).Cast<string>().ToArray();
        var controllers = ((IEnumerable)Field(native, "_controllers")).Cast<AnimatorControllerPlayable>().ToArray();
        return controllers.Select((controller, index) => {
            var state = controller.GetCurrentAnimatorStateInfo(0);
            return (object)new { backend = "ai-native", role = roles[index], selected, expectedHash = Animator.StringToHash(selected), stateHash = state.shortNameHash,
                normalizedTime = state.normalizedTime, length = state.length, speed = controller.GetSpeed(),
                clips = controller.GetCurrentAnimatorClipInfo(0).Select(clip => new { clip.clip.name, id = clip.clip.GetInstanceID(), length = clip.clip.length, clip.weight }).ToArray() };
        }).ToArray();
    }
    public void Dispose() { }
}
