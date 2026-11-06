using System;
using System.IO;
using System.Linq;
using MainGameSamabakeMotion;

internal static class Program
{
    private static int Main(string[] args)
    {
        string directory = args[0];
        var catalog = NativeCatalog.Read(Path.Combine(directory, "NativeHPoses.json"));
        if (catalog.poses.Length != 1) throw new Exception("First live trial must register exactly one pair");
        var pose = catalog.poses.Single();
        if (pose.states.Length != 6 || pose.female == null || pose.male == null || pose.female.file == pose.male.file) throw new Exception("Native phase/pair coverage differs");
        foreach (var actor in new[] { pose.female, pose.male }) NativeCatalog.CheckAsset(directory, actor);
        string temp = Path.Combine(directory, "invalid-test.json");
        string original = File.ReadAllText(Path.Combine(directory, "NativeHPoses.json"));
        int rejected = 0;
        foreach (string bad in new[] { "{}", "{\"version\":1,\"poses\":null}", "{\"version\":1,\"poses\":[]}", original.Replace("Orgasm_Loop", "missing_phase"), original.Replace(pose.male.file, "../outside.unity3d"), original.Replace("\"male\":{", "\"unexpected\":{") })
        {
            File.WriteAllText(temp, bad, new System.Text.UTF8Encoding(false));
            bool failed = false;
            try { NativeCatalog.Read(temp); } catch { failed = true; }
            if (!failed) throw new Exception("Invalid actual catalog was silently accepted");
            rejected++;
        }
        Console.WriteLine("PASS actual managed JSON reader: one native H pair, six states, both bundle hashes, " + rejected + " invalid/missing-data cases rejected");
        return 0;
    }
}
