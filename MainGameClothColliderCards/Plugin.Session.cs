using UnityEngine;
using SessionGate = CanonPlugins.Local.SessionGate;
namespace MainGameClothColliderCards {
public sealed partial class Plugin {
private void OnDestroy(){SessionGate.Dispose();}
private void BeginGateSession(){
foreach(var controller in ColliderCardController.Live) if(controller!=null) controller.GateStart();
}
private void EndGateSession(){
_makerWindowOpen=false; CaptureMakerInput(false); foreach(var controller in ColliderCardController.Live) if(controller!=null) controller.GateStop();
}
}
}
