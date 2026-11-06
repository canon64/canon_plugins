using System;

namespace MainGameAiShoujoMode.Conversation
{
    // Production and fixture execution use exactly the same handler composition.
    internal static class KksAdvHostPipeline
    {
        internal sealed class Devices
        {
            internal KksAdvProcessClock Clock;
            internal KksAdvActorHost.IBackend Actors;
            internal KksAdvBgmHost.IBackend Bgm;
            internal KksAdvSceneFadeHost.IBackend Fade;
            internal KksAdvCameraHost.IBackend Camera;
            internal KksAdvEventCgHost.IBackend EventCg;
            internal KksAdvPointHost.IBackend Points;
            internal KksAdvCrossFadeHost.IBackend CrossFade;
            internal Func<string, KksAdvCameraHost.Vector> FindVector;
        }
        internal static KksAdvMachine.IHost Build(KksAdvMachine.IHost core, Devices devices, Func<string, string> replace,
            KksConversationRuntimeSettings settings, Action changed)
        {
            if (devices == null) throw new ArgumentNullException(nameof(devices));
            KksAdvMachine.IHost commands = new KksAdvTimedHost(core, devices.Clock, replace, settings.zero);
            commands = new KksAdvActorHost(commands, devices.Actors, replace, settings);
            commands = new KksAdvBgmHost(commands, devices.Bgm, replace, settings.zero);
            commands = new KksAdvSceneFadeHost(commands, devices.Fade, replace, changed);
            commands = new KksAdvCameraHost(commands, devices.Camera, replace, devices.FindVector);
            commands = new KksAdvEventCgHost(commands, devices.EventCg, replace);
            commands = new KksAdvPointHost(commands, devices.Points, replace);
            return new KksAdvCrossFadeHost(commands, devices.CrossFade, replace);
        }
    }
}
