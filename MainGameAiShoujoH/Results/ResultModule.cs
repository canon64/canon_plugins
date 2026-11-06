using System;
using System.Collections.Generic;
using MainGameAiShoujoH.Contracts;

namespace MainGameAiShoujoH.Results
{
    public sealed class ResultDelta
    {
        public string OperationId { get; set; }
        public string ParticipantId { get; set; }
        public int Parameter { get; set; }
        public int Amount { get; set; }
        public bool Assignment { get; set; }
        public float? AssignedValue { get; set; }
    }
    public sealed class ResultReceipt
    {
        public bool Applied { get; set; }
        public bool Saved { get; set; }
        public string Error { get; set; }
    }
    public sealed class ResultServices
    {
        // The AI-world adapter must atomically deduplicate OperationId with the resident mutation.
        public Func<ResultDelta, ResultReceipt> Apply { get; set; }
        public Action<Guid> EndDialogue { get; set; }
    }
    public sealed class ResultModule : ISessionModule
    {
        private readonly ResultServices services;
        private readonly List<ResultDelta> pending = new List<ResultDelta>();
        private readonly HashSet<string> applied = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> awarded = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> nativeAwards = new HashSet<string>(StringComparer.Ordinal);
        private SessionContext context;
        private bool ended;
        private bool finalizing;
        public string Name => "results";
        public int PendingCount => pending.Count;
        public int AppliedCount => applied.Count;
        public ResultModule(ResultServices services) { this.services = services ?? throw new ArgumentNullException(nameof(services)); }
        public void Prepare(SessionContext value)
        {
            context = value ?? throw new ArgumentNullException(nameof(value));
            if (services.Apply == null) throw new InvalidOperationException("AI resident result persistence is not connected.");
        }
        public void AddParameter(string participant, int parameter, int amount, string awardKey)
        {
            if (ended || context == null || !context.IsCurrent() && !finalizing) return;
            if (parameter == -1) throw new ArgumentException("Gauge skill multipliers are not resident result parameters.");
            string key = participant + ":" + awardKey;
            if (!awarded.Add(key)) return;
            foreach (var delta in pending)
                if (delta.ParticipantId == participant && delta.Parameter == parameter)
                { delta.Amount = checked(delta.Amount + amount); return; }
            pending.Add(new ResultDelta { OperationId = "ai-h:" + context.Id.ToString("N") + ":" + pending.Count, ParticipantId = participant, Parameter = parameter, Amount = amount });
        }
        public void AddNativeAward(string participant, int mode, int pattern, Func<float> random)
        {
            if (ended || context == null || !context.IsCurrent() && !finalizing) return;
            if (random == null) throw new ArgumentNullException(nameof(random));
            string award = participant + ":" + mode + ":" + pattern;
            if (!nativeAwards.Add(award)) return;
            foreach (var row in NativeResultTable.Rows)
            {
                if (row[0] != mode || row[1] != pattern) continue;
                float n = random();
                if (float.IsNaN(n) || n < 0 || n > 1) throw new ArgumentException("Native result random must be in [0,1].");
                // Unity Mathf.RoundToInt uses midpoint-to-even.
                int amount = (int)Math.Round(row[3] + (row[4] - row[3]) * n, MidpointRounding.ToEven);
                AddParameter(participant, row[2], amount, "native:" + mode + ":" + pattern + ":" + row[2]);
            }
        }
        internal void CaptureFinalAwards(Action capture)
        { if (ended || context == null) return; finalizing = true; try { capture(); } finally { finalizing = false; } }
        public static float SkillMultiplier(int skill)
        {
            foreach (var row in NativeResultTable.Skills) if ((int)row[0] == skill && row[1] == -1) return row[2];
            return 1;
        }
        public void AddNativeSkill(string participant, int skill)
        {
            foreach (var row in NativeResultTable.Skills)
                if ((int)row[0] == skill && row[1] != -1) AddParameter(participant, (int)row[1], (int)Math.Round(row[2], MidpointRounding.ToEven), "skill:" + skill + ":" + row[1]);
        }
        public void Execute(Command command) { }
        public void End(EndReason reason)
        {
            if (ended) return;
            ended = true;
            // Config/cancellation/preparation failure deliberately bypass normal EndProc result/ADV.
            if (reason != EndReason.Completed) return;
            bool success = true;
            foreach (var delta in pending)
            {
                if (applied.Contains(delta.OperationId)) continue;
                try
                {
                    var receipt = services.Apply(delta);
                    if (receipt == null || !receipt.Applied || !receipt.Saved) { success = false; context.ReportFailure("AI H result " + delta.OperationId + ": " + (receipt?.Error ?? "No persistence receipt.")); }
                    else applied.Add(delta.OperationId);
                }
                catch (Exception ex) { context.TraceException?.Invoke(ex); success = false; context.ReportFailure("AI H result " + delta.OperationId + ": " + ex.Message); }
            }
            if (!success) return;
            // Ending ADV is awaited by the core ISessionFinalizer before participant restoration.
            if (services.EndDialogue != null) try { services.EndDialogue(context.Id); } catch (Exception ex) { context.ReportFailure("AI H ending scenario: " + ex.Message); }
        }
        public void Dispose() { ended = true; context = null; }
    }
}
