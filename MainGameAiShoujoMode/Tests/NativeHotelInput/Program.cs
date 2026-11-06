using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using MainGameAiShoujoMode;

static class Program
{
    static int checks;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static void Main()
    {
        bool owns = true, logicalReady = true;
        int calls = default, expected = default;
        var right = new NativeButtons(); var left = new NativeButtons();
        var scope = new NativeHotelInteractionScope(typeof(NativeButtons), () => owns, () => { if (logicalReady) calls++; });
        Check(!right.Down(Function.F3) && calls == ++expected, "controller F3 reaches hotel directly and does not send an OS key");
        Check(!right.Down(Function.F3) && calls == expected, "held F3 does not activate twice");
        Check(!right.Up(Function.F3), "consumed down has a consumed up");
        right.Down(Function.F3); Check(calls == ++expected, "next press activates once");
        owns = false;
        Check(!right.Up(Function.F3), "release remains owned after H or floor transition begins");
        Check(right.Down(Function.F3) && right.Up(Function.F3) && calls == expected, "outside hotel walking, native F3 is untouched");
        owns = true;
        Check(right.Down(Function.WALK) && right.Up(Function.WALK), "native walking input passes through");
        logicalReady = false; right.Down(Function.F3); right.Up(Function.F3);
        Check(calls == expected, "logical menu/load blocker can reject controller interaction");
        logicalReady = true;
        right.Down(Function.F3); left.Down(Function.F3);
        expected++; expected++;
        Check(calls == expected && !left.Up(Function.F3) && !right.Up(Function.F3), "each controller owns its matched press independently");
        try { new NativeHotelInteractionScope(typeof(NativeButtons), () => true, () => { }); throw new Exception("duplicate scope allowed"); }
        catch (InvalidOperationException) { Check(true, "duplicate hotel ownership is rejected"); }
        scope.Dispose(); scope.Dispose();
        Check(right.Down(Function.F3) && right.Up(Function.F3), "disposal removes both native input patches");
        using (var next = new NativeHotelInteractionScope(typeof(NativeButtons), () => true, () => calls++))
        { right.Down(Function.F3); right.Up(Function.F3); Check(calls == ++expected, "hotel can be entered again after cleanup"); }
        int writes = right.Actor.PositionWrites;
        using (var rotation = new NativeHotelInteractionScope(typeof(NativeButtons), () => owns, () => { }))
        {
            right.Turn(); right.Turn();
            Check(right.Actor.PositionWrites == writes && right.Actor.LastOnlyRotation, "hotel snap turns cannot write the player's floor position");
            right.Actor.MovePlayerToCamera(false);
            Check(right.Actor.PositionWrites == ++writes, "explicit position transfer outside snap turn stays native");
            owns = false; right.Turn();
            Check(right.Actor.PositionWrites == ++writes && !right.Actor.LastOnlyRotation, "normal VR snap turns remain native outside hotel ownership");
        }
        right.Turn();
        Check(right.Actor.PositionWrites == ++writes, "rotation patch is removed on hotel disposal");
        Console.WriteLine("PASS " + checks + " native hotel input and snap-turn checks using installed Harmony");
    }
}
enum Function { F3, WALK }
class NativeButtons
{
    int down, up;
    public readonly FakeActor Actor = new FakeActor();
    public bool Down(Function value) { int before = down; ButtonDown(value); return before != down; }
    public bool Up(Function value) { int before = up; ButtonUp(value); return before != up; }
    [MethodImpl(MethodImplOptions.NoInlining)] public void ButtonDown(Function value) { down++; }
    [MethodImpl(MethodImplOptions.NoInlining)] public void ButtonUp(Function value) { up++; }
    public void Turn() => Rotate();
    [MethodImpl(MethodImplOptions.NoInlining)] private void Rotate() { Actor.MovePlayerToCamera(false); }
}
class FakeActor
{
    public int PositionWrites;
    public bool LastOnlyRotation;
    [MethodImpl(MethodImplOptions.NoInlining)] public void MovePlayerToCamera(bool onlyRotation)
    {
        LastOnlyRotation = onlyRotation;
        if (!onlyRotation) PositionWrites++;
    }
}
