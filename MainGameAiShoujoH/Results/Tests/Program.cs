using System;
using System.Collections.Generic;
using MainGameAiShoujoH.Contracts;
using MainGameAiShoujoH.Results;

namespace MainGameAiShoujoH.Contracts
{
    public enum EndReason { Completed, Cancelled, PreparationFailed, PermissionRevoked, ModeDestroyed }
    public sealed class Command { }
    public sealed class SessionContext { public Guid Id; public Func<bool> IsCurrent; public Action<string> ReportFailure; }
    public interface ISessionModule : IDisposable { string Name { get; } void Prepare(SessionContext c); void Execute(Command c); void End(EndReason r); }
}
internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string label) { checks++; if (!condition) throw new Exception(label); }
    private static void Main()
    {
        var writes = new List<ResultDelta>(); var errors = new List<string>(); int adv = 0;
        var context = new SessionContext { Id = Guid.NewGuid(), IsCurrent = () => true, ReportFailure = errors.Add };
        var services = new ResultServices { Apply = delta => { writes.Add(delta); return new ResultReceipt { Applied = true, Saved = true }; }, EndDialogue = id => adv++ };
        var result = new ResultModule(services); result.Prepare(context);
        result.AddNativeAward("alice", 0, 0, () => 1); result.AddNativeAward("alice", 0, 0, () => 0);
        result.End(EndReason.Completed); result.End(EndReason.Completed);
        Check(writes.Count == 2, "native mode/pattern awards exactly once");
        Check(writes[0].Parameter == 11 && writes[0].Amount == 2 && writes[1].Parameter == 14 && writes[1].Amount == 3, "native S/L endpoints, not M column");
        Check(writes[0].OperationId != writes[1].OperationId && writes[0].OperationId.Contains(context.Id.ToString("N")), "persistent operation keys bind session and item");
        Check(adv == 1, "ending dialogue exactly once");
        var aggregate = new ResultModule(services); aggregate.Prepare(context); aggregate.AddParameter("alice", 100, 3, "a"); aggregate.AddParameter("alice", 100, 5, "b"); Check(aggregate.PendingCount == 1, "native ChangeParams aggregates same parameter before SetParamator"); aggregate.End(EndReason.Completed); Check(writes[2].Parameter == 100 && writes[2].Amount == 8, "desire is one summed SET delta, not multiple individual SETs");
        var cancel = new ResultModule(services); cancel.Prepare(context); cancel.AddParameter("alice", 1, 9, "cancel"); cancel.End(EndReason.Cancelled);
        Check(writes.Count == 3 && adv == 2, "cancel does not run normal result or end ADV");
        int attempts = 0;
        var partial = new ResultModule(new ResultServices { Apply = d => { attempts++; if (attempts == 1) throw new Exception("disk"); return new ResultReceipt { Applied = true, Saved = true }; }, EndDialogue = id => adv++ });
        partial.Prepare(context); partial.AddParameter("alice", 1, 2, "one"); partial.AddParameter("alice", 2, 2, "two"); partial.End(EndReason.Completed);
        Check(attempts == 2 && errors.Count == 1, "independent results continue after first failure");
        Check(adv == 2, "partial save does not claim complete and begin ADV");
        var stale = new ResultModule(services); stale.Prepare(new SessionContext { Id = Guid.NewGuid(), IsCurrent = () => false, ReportFailure = errors.Add }); stale.AddParameter("alice", 1, 99, "late"); stale.End(EndReason.Completed);
        Check(writes.Count == 3, "stale callback cannot add result");
        Console.WriteLine("NATIVE_RESULT_CHECKS_PASS " + checks);
    }
}
