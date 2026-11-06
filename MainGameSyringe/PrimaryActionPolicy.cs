namespace MainGameSyringe
{
 public enum SyringeAction { Wait, Spawn, Withdraw, Refill, Insert, Inject }
 public static class PrimaryActionPolicy
 {
  public static SyringeAction Decide(bool spawned,bool inserted,bool spent,bool busy)
   =>busy?SyringeAction.Wait:!spawned?SyringeAction.Spawn:inserted?(spent?SyringeAction.Withdraw:SyringeAction.Inject):spent?SyringeAction.Refill:SyringeAction.Insert;
 }
}
