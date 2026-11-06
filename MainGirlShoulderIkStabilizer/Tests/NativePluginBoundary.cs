namespace MainGirlShoulderIkStabilizer
{
    public sealed partial class ShoulderIkStabilizerPlugin
    {
        public const string Guid="diagnostic-test";
        public static ShoulderIkStabilizerPlugin Instance;
        private readonly PluginSettings settings=new();
        private bool session;
        public readonly List<string> Records=new();
        private void WriteDiagnostic(string record) {if(settings.ShoulderDiagnosticLog)Records.Add(record);}
        private void OnShoulderPoseFaulted(Exception ex) {if(settings.ShoulderDiagnosticLog)Records.Add(ex.ToString());}
        private void ReportSkinBinding(Dictionary<string,UnityEngine.Transform> bodyBones) { }
        public void Configure(bool enabled,ChaControl actor,bool active=true)
        {
            Instance=this;settings.ShoulderDiagnosticLog=enabled;session=active;
            diagnosticActor=actor;RefreshNativeDiagnostics();
        }
        public Exception SimulateNative(ExpressionBone expression,Exception failure=null)
        {
            BeforeNativeExpression(expression,out long token);
            return AfterNativeExpression(expression,token,failure);
        }
        public void ObserveIk(string stage) => RecordNativeOrder(stage);
    }
}
