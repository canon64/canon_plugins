using System;
using UnityEngine;
using RootMotion.FinalIK;
namespace UnityEngine
{
 public struct Vector3 {public float x;public Vector3(float value){x=value;}public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x);}
 public struct Quaternion {public float x;}
 public class Transform { }
 public class Animator
 {
  public FullBodyBipedIK Ik=new();public T GetComponent<T>() where T:class=>Ik as T;public T GetComponentInChildren<T>(bool inactive) where T:class=>Ik as T;
 }
}
public class ChaControl {public Animator animBody=new();}
namespace RootMotion.FinalIK
{
 public enum FullBodyBipedEffector {LeftShoulder,RightShoulder}
 public class IKEffector {public Transform target=new();public Vector3 position;public Quaternion rotation;public float positionWeight=.2f,rotationWeight=.3f;}
 public class Solver
 {
  public Action OnPreUpdate,OnPostUpdate;public IKEffector Left=new(),Right=new();
  public IKEffector GetEffector(FullBodyBipedEffector e)=>e==FullBodyBipedEffector.LeftShoulder?Left:Right;
 }
 public class FullBodyBipedIK {public Solver solver=new();}
}
namespace CanonPlugins.Shared {public static class PluginSessionGate {public static bool Allowed=true;}}
namespace MainGameVrNeckChain
{
 public sealed partial class Plugin
 {
  class Pull {public bool Active;}
  ChaControl _female=new();Pull _shoulderPull=new();bool _pullHasPose=true,_chainVisualPending;
  Vector3 _pullLeftPosition=new(1),_pullRightPosition=new(2),_pullOffset=new(.25f);
  Quaternion _pullLeftRotation=new(){x=.1f},_pullRightRotation=new(){x=.2f};
  public bool isActiveAndEnabled=true;public Solver Native=>_female.animBody.Ik.solver;
  public bool VisualPending=>_chainVisualPending;
  void UpdateShoulderPull(){}
  public void Start()=>BindPullSolver();public void Stop()=>UnbindPullSolver();public void Tension(bool active)=>_shoulderPull.Active=active;
 }
}
static class Program
{
 static int checks;static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static void Main()
 {
  var p=new MainGameVrNeckChain.Plugin();p.Start();var solver=p.Native;var left=solver.Left;var right=solver.Right;
  var leftTarget=left.target;var rightTarget=right.target;left.position=new(9);right.position=new(8);
  solver.OnPreUpdate();Check(left.target==leftTarget&&left.positionWeight==.2f,"slack leaves existing IK controls intact");
  p.Tension(true);solver.OnPreUpdate();
  Check(left.target==null&&right.target==null,"native target cannot overwrite temporary chain goals");
  Check(left.position.x==1.25f&&right.position.x==2.25f,"actual effector goals receive captured shoulder pose plus pull displacement");
  Check(left.positionWeight==1&&right.rotationWeight==1&&left.rotation.x==.1f,"pull applies position and rotation weights");
  solver.OnPostUpdate();Check(left.target==leftTarget&&right.target==rightTarget&&left.position.x==9&&right.position.x==8,"post-solve restores other owners' targets and positions");
  Check(left.positionWeight==.2f&&left.rotationWeight==.3f&&p.VisualPending,"post-solve restores weights and schedules final chain rendering");
  solver.OnPreUpdate();p.Stop();Check(left.target==leftTarget&&left.positionWeight==.2f,"stop during a solve restores pending inputs");
  Check(solver.OnPreUpdate==null&&solver.OnPostUpdate==null,"cleanup unsubscribes native solver events");
  p.Start();CanonPlugins.Shared.PluginSessionGate.Allowed=false;solver.OnPreUpdate();Check(left.target==leftTarget,"revoked permit cannot move shoulders");
  p.Stop();Console.WriteLine("PASS "+checks+" production chain solver wiring checks; native FinalIK solve is not executed.");
 }
}
