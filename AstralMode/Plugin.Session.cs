using UnityEngine;
using SessionGate = CanonPlugins.Local.SessionGate;
namespace AstralMode {
public sealed partial class Plugin {
private void BeginGateSession(){
_hSceneProc=SessionGate.HScene; _hFlag=SessionGate.ReadScene<HFlag>("flags"); _inHScene=true; _simultOrgasmCount=0;
}
private void EndGateSession(){
StopAllCoroutines(); var work=ForceExitCoroutine(); try{while(work.MoveNext()){} }finally{(work as System.IDisposable)?.Dispose();} _hSceneProc=null; _hFlag=null; _inHScene=false; _simultOrgasmCount=0;
}
}
}
