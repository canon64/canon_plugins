namespace CanonPlugins.Shared
{
    internal sealed class DedicatedVrButton : IDisposable
    {
        internal static Valve.VR.SteamVR_Action_Boolean Current;
        internal Valve.VR.SteamVR_Action_Boolean Action { get; }
        internal DedicatedVrButton(string name)
        {
            if(name!="itemdelete")throw new Exception("incorrect physical B action set");
            Action=Current=new();
        }
        public void Dispose() { }
    }
}
