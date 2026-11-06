using UnityEngine;
namespace H {
 public class HPointData:MonoBehaviour {
  public MeshRenderer _meshRender;
  public int[] _categorys=new int[0];public string[] _targets=new string[0],_groups=new string[0];
  public Vector3 _offsetPos,_offsetAngle;
  public int SetWorldToLocalOffsetButton,BackUpPositionButton,MoveOffsetButton,ResetPositionButton,_experience,SetMapNameSameButton;
 }
}
