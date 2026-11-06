using System;
using MainGameAiShoujoH.Contracts;
using MainGameAiShoujoH.Integration;
using UnityEngine;

static class Program
{
    static int checks;
    static void Check(bool result, string description) { if (!result) throw new Exception(description); ++checks; }
    static void Main()
    {
        var origin = new Vector3(32, 8, -45);
        Check(HPointRange.Contains(origin, origin), "Current point");
        Check(HPointRange.Contains(origin, origin + new Vector3(6, 8, 0)), "Exactly 10m, including height");
        Check(!HPointRange.Contains(origin, origin + new Vector3(6, 8.01f, 0)), "Beyond 10m in 3D");
        Check(!HPointRange.Contains(origin, origin + new Vector3(10.01f, 0, 0)), "No horizontal overflow");
        Check(!HPointRange.Contains(origin, origin + new Vector3(0, 11, 0)), "No distant floor above");
        Check(!HPointRange.Contains(origin, new Vector3(float.NaN, 0, 0)), "NaN rejected");
        Check(!HPointRange.Contains(origin, new Vector3(float.PositiveInfinity, 0, 0)), "Infinity rejected");
        var ordinary = new[] { 0, 1 };
        Check(NativeKksEndingDecision.Select(true, 0, 0, ordinary, 0, 0, false) == 0, "First insertion precedes no-climax branch");
        Check(NativeKksEndingDecision.Select(false, 0, 1, ordinary, 0, 0, false) == 6, "No female climax with male finish");
        Check(NativeKksEndingDecision.Select(false, 0, 0, ordinary, 3, 0, false) == 7, "Experienced no-climax branch");
        Check(NativeKksEndingDecision.Select(false, 1, 0, ordinary, 0, 0, false) == 8, "Female climax without male finish");
        Check(NativeKksEndingDecision.Select(false, 3, 1, ordinary, 0, 0, false) == 4, "Repeated female climax");
        Check(NativeKksEndingDecision.Select(false, 1, 1, ordinary, 2, 0, true) == 11, "Invited result precedes normal result");
        Check(NativeKksEndingDecision.Select(false, 1, 1, ordinary, 2, 0, false) == 2, "Normal result after other native poses");
        Check(NativeKksEndingDecision.Select(false, 0, 1, new[] { 12 }, 0, 0, false) == 1, "Special category keeps native EndProc condition");
        Console.WriteLine("HPOINT_RANGE_AND_NATIVE_ENDING_PASS " + checks);
    }
}
