using System;
using System.Linq;
using UnityEngine;

namespace MainGameAiShoujoMode.Conversation
{
    internal static class KksAdvNativeSession
    {
        private sealed class Release : IDisposable
        {
            internal Action Close;
            public void Dispose() { var close = Close; Close = null; close?.Invoke(); }
        }
        private sealed class ViewHost : KksAdvMachine.IHost
        {
            private readonly KksConversationView _view;
            internal ViewHost(KksConversationView view) { _view = view; }
            public bool IsRegulated => _view.LogVisible;
            // The existing view currently displays a complete caption; finite typewriter/pages remain separate work.
            public bool TextComplete => true;
            public void CompleteText() { }
            public void ClearText() { }
            public void ClearCurrentCharaData() { }
            public KksAdvMachine.ICommand Create(KksAdvMachine.Context context) => throw new NotSupportedException("KKS ADV command is not connected: " + context.Entry.Row.Command);
        }
        internal static IKksAdvSession Create(MonoBehaviour host, Camera camera, KksNativeConversationSession session,
            KksActorPresentation primary, KksConversationView view, KksVoicePlayback voices, KksVoiceBundleContext bundles,
            KksConversationStateStore store, string contentDirectory, Func<IDisposable> suspendMusic, Action<string> log)
        {
            if (camera == null) throw new InvalidOperationException("AI conversation camera is unavailable.");
            var lifetime = new KksAdvSessionScope(); var settings = session.Settings;
            try
            {
                var actors = lifetime.Own(new KksAdvActorRegistry(session, primary, log));
                // This is its own overlay canvas; nesting under the message canvas would disable its root scaler.
                var points = lifetime.Own(new KksAdvNativePointBackend(host, host.transform, contentDirectory, store,
                    value => session.Player.koikatsuPoint = value, settings.zero, settings.koikatsuPointMax));
                var bgm = lifetime.Own(new KksAdvBgmPlayback(new KksAdvNativeBgmDevice(host.transform, settings, log), suspendMusic, settings.advBgmMinimumFadeSeconds));
                var assets = new KksAdvCatalog(); KksAdvOwnedSession owned = null;
                owned = new KksAdvOwnedSession((bundle, asset) => assets.Load(bundle, asset, null), (run, script) =>
                {
                    log?.Invoke("KKS_ADV_NATIVE_RUN_CREATE resident=" + session.Identity + " bundle=" + script.Bundle + " asset=" + script.Asset);
                    run.Own(new Release { Close = () => log?.Invoke("KKS_ADV_NATIVE_RUN_SCOPE_EXIT bundle=" + script.Bundle + " asset=" + script.Asset) });
                    KksAdvMachine activeMachine = null;
                    KksAdvMachine.IHost pipeline = null;
                    var text = TextContext(session); text.SetFile(script.Bundle, script.Asset);
                    var rig = run.Own(new KksAdvRenderRig(camera, actors, actors.CameraParticipants, depthOfFieldEnabled: false, settings: settings));
                    bool correctCamera = true;
                    var cg = lifetime.Own(new KksAdvNativeEventCgBackend(actors, camera, new KksAdvEventCgPlacement(actors, settings), actors.CameraParticipants,
                        () => correctCamera, value => correctCamera = value,
                        () => rig.StartImplicitCrossFade(-settings.one, exception => activeMachine?.PresentationFailed(exception)), settings));
                    run.Own(new Release { Close = cg.EndRun });
                    run.Own(new KksAdvParticipantPresentation(actors, rig, cg));
                    void RefreshAdvance()
                    {
                        var machine = activeMachine;
                        if (machine == null || !ReferenceEquals(owned.CurrentMachine, machine)) return;
                        view.SetCanAdvance(machine.HasText && !machine.HardWaiting && pipeline?.IsRegulated != true);
                    }
                    void Changed()
                    {
                        var machine = activeMachine;
                        if (machine != null && ReferenceEquals(owned.CurrentMachine, machine)) machine.NotifyPresentationChanged();
                    }
                    var fades = run.Own(new KksAdvNativeSceneFadeBackend(host, Changed));
                    var scheduler = new KksUnityDeadlineScheduler(host);
                    var clock = run.Own(new KksAdvProcessClock(() => scheduler.Now, scheduler.Schedule, settings.zero));
                    var mediaBackend = run.Own(new KksAdvNativeMediaBackend(view, voices, bundles, actors.Resolve,
                        (motions, completed, failed) => throw new NotSupportedException("KKS motion-completion events are not connected."),
                        new ViewHost(view), new ADV.Info.Audio(), new ADV.Info.Anime.Play(), log, Changed));
                    var media = new KksAdvMediaHost(mediaBackend, text.ReplaceArgument, text.ReplaceText, settings.zero, settings.one);
                    KksAdvMachine.IHost commands = new KksAdvCoreHost(media, assets, text.ReplaceArgument, text.SetFile, log, settings.zero);
                    commands = KksAdvHostPipeline.Build(commands, new KksAdvHostPipeline.Devices
                    {
                        Clock = clock, Actors = new KksAdvNativeActorBackend(actors, name => FindTarget(actors, camera, name, settings.one)),
                        Bgm = bgm, Fade = fades, Camera = rig.Commands, EventCg = cg, Points = points, CrossFade = rig,
                        FindVector = name => null
                    }, text.ReplaceArgument, settings, Changed);
                    pipeline = commands;
                    view.DisplayStateChanged += Changed;
                    run.Own(new Release { Close = () => view.DisplayStateChanged -= Changed });
                    view.SetCanAdvance(false);
                    return activeMachine = new KksAdvMachine(commands, settings.zero, settings.one, clock.SetSuspended,
                        (entries, ready, failed) => { view.SetCanAdvance(false); return rig.PrepareBatch(entries, ready, failed); }, RefreshAdvance);
                }, lifetime);
                log?.Invoke("KKS_ADV_NATIVE_SESSION_CREATED resident=" + session.Identity + " dof=false");
                return owned;
            }
            catch { lifetime.Dispose(); throw; }
        }
        private static Transform FindTarget(KksAdvActorRegistry actors, Camera camera, string name, int one)
        {
            if (camera != null && camera.transform.name == name) return camera.transform;
            var candidates = actors.CameraParticipants().SelectMany(actor => actor.GetComponentsInChildren<Transform>(true)).Where(point => point.name == name).Distinct().ToArray();
            if (candidates.Length == default(int)) throw new InvalidOperationException("ADV target transform is missing: " + name);
            if (candidates.Length != one) throw new InvalidOperationException("ADV target transform is ambiguous: " + name);
            return candidates.First();
        }
        private static KksAdvTextContext TextContext(KksNativeConversationSession session)
        {
            var context = new KksAdvTextContext(new[] { "P", "H" }.SelectMany(prefix => SaveData.CharaData.Names.Select(name => prefix + name)),
                key => key == "P" ? key + SaveData.CharaData.Names[(int)session.Player.nameType]
                    : key == "H" ? key + SaveData.CharaData.Names[(int)session.Heroine.nameType] : null);
            foreach (var data in session.Player.param.datas.Concat(session.Heroine.param.datas))
            {
                if (data.command == SaveData.CharaData.Params.Data.Command.Replace) context.Replacements[data.key] = () => (string)data.value;
                else if (data.command != SaveData.CharaData.Params.Data.Command.None) context.Variables[data.key] = () => data.value.ToString();
            }
            return context;
        }
    }
}
