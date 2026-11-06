using System;
using UnityEngine;

namespace MainGameSyringe
{
 public sealed partial class Plugin
 {
  GameObject _returnBoundaryObject,_returnHandObject;
  ReturnBoundary _returnBoundary;
  Vector3 _returnAnchorLocal;
  const float HandProbeRadius=.0001f;

  void ArmReturnBoundary(bool captureAnchor=false)
  {
   ClearReturnBoundary();
   if(!Allowed||!InteractionFocused||_insertedBone==null||_model==null||_heldController==null||_focus==null||!_focus.IsValid)return;
   // Capture after the insertion snap (or regrab), so neither the original
   // controller/item offset nor the snap itself counts as pulling the hand away.
   if(captureAnchor)_returnAnchorLocal=_model.transform.InverseTransformPoint(_heldController.transform.position);
   try
   {
    // Native physics emits exit edges. No pose subscriptions or polling callbacks.
    _returnHandObject=new GameObject("Syringe held hand point");
    _returnHandObject.layer=2;
    _returnHandObject.transform.SetParent(_heldController.transform,false);
    SetWorldScale(_returnHandObject.transform);
    var hand=_returnHandObject.AddComponent<SphereCollider>();hand.radius=HandProbeRadius;hand.isTrigger=true;hand.contactOffset=.00001f;
    var body=_returnHandObject.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;
    _returnBoundaryObject=new GameObject("Syringe return boundary");
    _returnBoundaryObject.layer=2;
    _returnBoundaryObject.transform.SetParent(_model.transform,false);
    _returnBoundaryObject.transform.localPosition=_returnAnchorLocal;
    SetWorldScale(_returnBoundaryObject.transform);
    var sphere=_returnBoundaryObject.AddComponent<SphereCollider>();sphere.radius=Mathf.Max(HandProbeRadius,_pullDistance.Value-HandProbeRadius);sphere.isTrigger=true;sphere.contactOffset=.00001f;
    _returnBoundary=_returnBoundaryObject.AddComponent<ReturnBoundary>();
    _returnBoundary.Hand=hand;_returnBoundary.Exited=ReturnBoundaryExited;
    // Rearming settings preserves the anchor and still checks the current distance.
    ReturnIfOutside();
   }
   catch{ClearReturnBoundary();throw;}
  }
  static void SetWorldScale(Transform item)
  {
   var scale=item.parent.lossyScale;
   item.localScale=new Vector3(1f/Mathf.Max(.000001f,Mathf.Abs(scale.x)),1f/Mathf.Max(.000001f,Mathf.Abs(scale.y)),1f/Mathf.Max(.000001f,Mathf.Abs(scale.z)));
  }
  bool ReturnIfOutside()
  {
   if(_model==null||_heldController==null||_insertedBone==null)return false;
   float distance=Vector3.Distance(_heldController.transform.position,_model.transform.TransformPoint(_returnAnchorLocal));
   if(!SkinGeometry.ShouldRelease(distance,_pullDistance.Value))return false;
   DetachInsertion(true);
   _status="手がしきい値以上離れたため、注射器を手元へ戻しました。投与していません。";
   return true;
  }
  void ReturnBoundaryExited()
  {
   if(!Allowed||!InteractionFocused||!_vrFocused||_focus==null||!_focus.IsValid)return;
   try{ReturnIfOutside();}
   catch(Exception ex){EndGrab(true);if(_diagnostics.Value)Trace("手元への復帰失敗: "+ex);}
  }
  void ClearReturnBoundary()
  {
   if(_returnBoundary!=null){_returnBoundary.Exited=null;_returnBoundary.Hand=null;}_returnBoundary=null;
   var boundary=_returnBoundaryObject;_returnBoundaryObject=null;
   var hand=_returnHandObject;_returnHandObject=null;
   if(boundary!=null){boundary.SetActive(false);Destroy(boundary);}
   if(hand!=null){hand.SetActive(false);Destroy(hand);}
  }
 }
 public sealed class ReturnBoundary:MonoBehaviour
 {
  internal Collider Hand;internal Action Exited;
  void OnTriggerExit(Collider other){if(other==Hand)Exited?.Invoke();}
 }
}
