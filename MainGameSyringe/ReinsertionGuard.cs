namespace MainGameSyringe
{
 // Suppress only a needle still at its last puncture, never an entire limb or actor.
 public sealed class ReinsertionGuard
 {
  public bool Blocked {get;private set;}
  public void Withdraw(float tipDistance,float clearance){Blocked=tipDistance<=clearance;}
  public bool CanInsert(float tipDistance,float clearance,bool force=false)
  {
   if(force||tipDistance>clearance)Blocked=false;
   return !Blocked;
  }
  public void Reset(){Blocked=false;}
 }
}
