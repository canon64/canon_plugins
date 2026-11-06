using UnityEngine;
using SessionGate = CanonPlugins.Local.SessionGate;
namespace MainGirlShoulderIkStabilizer {
public sealed partial class ShoulderIkStabilizerPlugin {
private void BeginGateSession(){
session = true;
ReportDiagnosticState("gate_begin");
MainGirlHipHijack.Plugin.ShoulderRuntimeChanged += OnRuntimeChanged;
MainGirlHipHijack.Plugin.ArmIkRunningChanged += OnArmChanged;
if(settings.ShoulderDiagnosticLog) MainGirlHipHijack.Plugin.ShoulderPoseFaulted += OnShoulderPoseFaulted;
if(MainGirlHipHijack.Plugin.TryGetShoulderRuntime(out var actor, out var ik)) OnRuntimeChanged(actor, ik);
}
private void EndGateSession(){
if(session && settings.ShoulderDiagnosticLog) WriteDiagnostic("SESSION_END gate="+SessionGate.Allowed);
session = false;
nativeCorrection?.Dispose(); nativeCorrection = null;
diagnosticActor = null;
RefreshNativeDiagnostics();
MainGirlHipHijack.Plugin.ShoulderRuntimeChanged -= OnRuntimeChanged;
MainGirlHipHijack.Plugin.ArmIkRunningChanged -= OnArmChanged;
MainGirlHipHijack.Plugin.ShoulderPoseFaulted -= OnShoulderPoseFaulted;
UnsubscribePose(); rotator?.Dispose(); rotator=null; currentIk=null;
}
}
}
