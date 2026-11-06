namespace MainGameVoiceFaceEventBridge
{
    internal static partial class SessionGate
    {
        // Native H startup can publish the gate before actors/voice have been assigned.
        // Refresh only on an actual contact, using the already-held scene.
        internal static void RefreshContactState()
        {
            if(!Allowed||Scene==null)return;
            var runtime=new CanonPlugins.Shared.HSceneRuntime(Scene);
            Flags=runtime.Flags;Voice=runtime.Voice;
            _females.Clear();
            if(runtime.Females!=null)
                foreach(var value in runtime.Females)
                    if(value is ChaControl actor&&actor!=null&&actor.sex==1&&!_females.Contains(actor))_females.Add(actor);
        }
    }
}
