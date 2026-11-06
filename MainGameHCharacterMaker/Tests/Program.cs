using System;
using MainGameHCharacterMaker;
using MainGameBlankMapAdd;

internal static class Program
{
    private sealed class Target : IShapeTarget
    {
        internal float[] Body = { 1.6f, -0.3f, 0.5f }, Face = { 0.1f, 1.7f };
        internal bool Alive = true, ThrowOnce;
        internal int Writes, Refreshes;
        public bool IsAlive => Alive;
        public float[] Read(bool body) => body ? Body : Face;
        public bool Write(bool body, int index, float value)
        {
            (body ? Body : Face)[index] = value; Writes++;
            if (ThrowOnce) { ThrowOnce = false; throw new Exception("native failure after mutation"); }
            return true;
        }
        public void Refresh(bool body, bool face) { Refreshes++; }
    }
    private static int _checks;
    private static void Check(bool ok, string name)
    {
        if (!ok) throw new Exception("FAIL " + name);
        _checks++; Console.WriteLine("PASS " + name);
    }
    private static void Main()
    {
        var target = new Target(); bool permitted = true;
        var edit = new ShapeEditSession(target, () => permitted);
        Check(target.Writes == 0 && edit.Body[0] == 1.6f && edit.Body[1] == -0.3f, "opening preserves out-of-range values without writes");
        Check(edit.Preview(true, 0, 2.2f) && target.Body[0] == 2.2f, "preview writes actual body target without clamp");
        Check(edit.Preview(false, 1, -0.2f) && target.Face[1] == -0.2f, "preview writes face target");
        Check(!edit.Preview(true, -1, 1) && !edit.Preview(true, 99, 1), "invalid indices rejected");
        int writes = target.Writes;
        Check(!edit.Preview(true, 0, float.NaN) && !edit.Preview(true, 0, float.PositiveInfinity) && target.Writes == writes, "nonfinite values never reach game setter");
        target.Body[2] = 0.9f; // A different plugin changes an untouched field during editing.
        permitted = false;
        Check(!edit.Preview(true, 0, 0.3f) && !edit.Commit(), "expired permit blocks editing and commit");
        edit.Cancel();
        Check(target.Body[0] == 1.6f && target.Face[1] == 1.7f, "rollback runs even after permit expires");
        Check(target.Body[2] == 0.9f, "rollback preserves unrelated live changes");
        writes = target.Writes; edit.Cancel();
        Check(target.Writes == writes && !edit.Preview(true, 0, 1), "double cleanup and stale UI cannot mutate target");
        permitted = true; edit = new ShapeEditSession(target, () => permitted);
        edit.Preview(true, 0, 0.6f); Check(edit.Commit(), "explicit commit succeeds");
        edit.Cancel(); Check(target.Body[0] == 0.6f, "committed edits survive window cleanup");
        edit = new ShapeEditSession(target, () => permitted); target.ThrowOnce = true;
        try { edit.Preview(true, 0, 0.8f); } catch (Exception) { }
        edit.Cancel(); Check(target.Body[0] == 0.6f, "partially failing native setter remains rollbackable");
        edit = new ShapeEditSession(target, () => permitted); target.Alive = false;
        Check(!edit.CanCommit && !edit.Preview(true, 0, 0.5f), "destroyed character rejects preview and commit");
        edit.Cancel(); Check(edit.Closed, "destroyed-character cleanup finishes");
        bool available = false; int opens = 0;
        var registration = CharacterMakerBarApi.Register(() => available, () => opens++);
        CharacterMakerBarApi.Open(); Check(opens == 0 && !CharacterMakerBarApi.IsAvailable, "button unavailable outside a permitted session");
        available = true; CharacterMakerBarApi.Open(); Check(opens == 1, "button opens registered editor");
        try { CharacterMakerBarApi.Register(() => true, () => { }); throw new Exception("duplicate accepted"); }
        catch (InvalidOperationException) { Check(true, "duplicate button provider rejected"); }
        registration.Dispose(); registration.Dispose(); CharacterMakerBarApi.Open();
        Check(!CharacterMakerBarApi.IsAvailable && opens == 1, "disposed button provider cannot open stale editor");
        using (CharacterMakerBarApi.Register(() => true, () => opens++)) CharacterMakerBarApi.Open();
        Check(opens == 2, "next provider can register after disposal");
        CheckGateLifecycle();
        AppearanceChecks.Run(Check);
        CardSaveChecks.Run(Check);
        Console.WriteLine("TOTAL " + _checks + " PASSED");
    }

    private static void CheckGateLifecycle()
    {
        const string guid = "canon.kks.hcharactermaker";
        var policy = new CanonGate.ExceptionPolicy(); policy.Load("", "");
        var target = new Target();
        ShapeEditSession edit = null;
        CanonGate.GateApi.Permit permit = default;
        int starts = 0, stops = 0;
        bool partialFailure = false;
        CanonGate.GateApi.SetRunning(true);
        using (CanonGate.GateApi.Register(guid, () =>
        {
            if (CanonGate.GateApi.Current.Kind != CanonGate.GateApi.SessionKind.H) return;
            starts++; permit = CanonGate.GateApi.Capture(guid);
            var held = permit;
            edit = new ShapeEditSession(target, () => held.IsValid);
            if (partialFailure) { edit.Preview(true, 0, 0.4f); throw new Exception("partial startup"); }
        }, () => { stops++; edit?.Cancel(); }))
        {
            CanonGate.GateApi.BeginSession(CanonGate.GateApi.SessionKind.H, false, policy, new HSceneProc());
            Check(starts == 0 && edit == null, "real gate denial cannot start an edit transaction");
            CanonGate.GateApi.EndSession(CanonGate.GateApi.SessionKind.H);
            CanonGate.GateApi.BeginSession(CanonGate.GateApi.SessionKind.H, true, policy, new HSceneProc());
            Check(starts == 1 && edit.Preview(true, 0, 0.7f), "real gate grant enables preview");
            var oldEdit = edit; var oldPermit = permit;
            CanonGate.GateApi.BeginSession(CanonGate.GateApi.SessionKind.H, true, policy, new HSceneProc());
            Check(starts == 1, "duplicate H start does not replace the transaction");
            CanonGate.GateApi.EndSession(CanonGate.GateApi.SessionKind.H);
            Check(stops == 1 && target.Body[0] == 1.6f && !oldPermit.IsValid, "gate revokes first and rollback still restores actor");
            CanonGate.GateApi.EndSession(CanonGate.GateApi.SessionKind.H);
            Check(stops == 1, "duplicate H end does not repeat cleanup");
            CanonGate.GateApi.BeginSession(CanonGate.GateApi.SessionKind.H, true, policy, new HSceneProc());
            Check(starts == 2 && !oldPermit.IsValid && !oldEdit.Preview(true, 0, 0.1f), "next H cannot revive old editor or permit");
            CanonGate.GateApi.EndSession(CanonGate.GateApi.SessionKind.H);
            partialFailure = true;
            CanonGate.GateApi.BeginSession(CanonGate.GateApi.SessionKind.H, true, policy, new HSceneProc());
            Check(starts == 3 && stops == 3 && target.Body[0] == 1.6f && !permit.IsValid, "partial-startup failure rolls back through real gate cleanup");
            CanonGate.GateApi.EndSession(CanonGate.GateApi.SessionKind.H);
        }
        CanonGate.GateApi.SetRunning(false);
    }
}
