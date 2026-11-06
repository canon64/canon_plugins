using System;
using System.IO;
using MainGameBlankMapAdd;

internal static class ManifestTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("AI_ISLAND_TEST_MANIFEST");
        if (string.IsNullOrEmpty(path)) throw new Exception("AI_ISLAND_TEST_MANIFEST must point to the deployed manifest");
        string json = File.ReadAllText(path);
        var manifest = AiIslandManifest.Read(json);
        if (manifest.files.Length != 7 || manifest.files[6].bytes != new FileInfo(Path.Combine(Path.GetDirectoryName(path), manifest.files[6].path)).Length)
            throw new Exception("Real deployed manifest failed");
        Console.WriteLine("PASS actual deployed manifest, native metadata and 64-bit file sizes");
        Reject("null", "ルート");
        Reject("{invalid", "JSON");
        Reject(json.Replace("map_00_data\"", "wrong_scene\""), "scene");
        Reject(json.Replace("__AI_KKS_Start", "wrong_anchor"), "anchor");
        Reject(json.Replace("map_00_data.unity3d", "other.unity3d"), "末尾");
        Reject(json.Replace(manifest.files[0].path, manifest.files[1].path), "パス重複");
        Reject(json.Replace(manifest.files[0].sha256, "not-a-hash"), "sha256");
        // Missing fields must fail with diagnostics, not a null dereference.
        Reject("{\"version\":1,\"scene\":\"map_00_data\",\"root\":\"map00_Beach\",\"anchor\":\"__AI_KKS_Start\"}", "files件数");
    }
    private static void Reject(string json, string expected)
    {
        try { AiIslandManifest.Read(json); }
        catch (InvalidDataException ex)
        {
            if (!ex.Message.Contains(expected)) throw new Exception("Missing field diagnostic: " + ex.Message);
            Console.WriteLine("PASS rejected manifest: " + expected);
            return;
        }
        throw new Exception("Accepted invalid manifest: " + expected);
    }
}
