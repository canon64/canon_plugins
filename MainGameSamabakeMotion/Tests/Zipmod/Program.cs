using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using System.Xml.Serialization;
using AnimationLoader;
using MainGameSamabakeMotion;

internal static class Program
{
    private static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
        {
            string file = new AssemblyName(e.Name).Name + ".dll";
            foreach (string directory in new[] { "F:/kks/BepInEx/plugins", "F:/kks/BepInEx/core", "F:/kks/KoikatsuSunshine_Data/Managed", "F:/kks/BepInEx/plugins/KKAPI" })
            {
                string path = Path.Combine(directory, file);
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        };
        if (args.Length == 2) { RunAll(args[0], args[1]); return 0; }
        Run(args[0]);
        Console.WriteLine("PASS installed AnimationLoader XML serializer: KKS-only, aibu, donor, stable identity, both bundle references, 6 identity rejection cases");
        return 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunAll(string directory, string catalogPath)
    {
        var catalog = MotionDefinitions.Read(catalogPath);
        int count = 0;
        var seen = new System.Collections.Generic.HashSet<string>();
        foreach (string path in Directory.GetFiles(directory, "*.zipmod"))
        using (var archive = ZipFile.OpenRead(path))
        {
            XDocument doc;
            using (var stream = archive.GetEntry("manifest.xml").Open()) doc = XDocument.Load(stream);
            string guid = doc.Root.Element("guid").Value;
            if (doc.Root.Element("AnimationLoader").Elements("Animation").Any()) throw new Exception("Package is not KKS-only");
            foreach (var node in doc.Root.Element("AnimationLoader").Element("KoikatsuSunshine").Elements("Animation"))
            {
                SwapAnimationInfo value;
                using (var reader = node.CreateReader()) value = (SwapAnimationInfo)new XmlSerializer(typeof(SwapAnimationInfo)).Deserialize(reader);
                value.Guid = guid;
                var definition = catalog.Single(p => p.guid == guid && p.studioId == value.StudioId);
                if (!definition.Matches(value) || definition.name != value.AnimationName || !seen.Add(guid + ":" + value.StudioId)) throw new Exception("Manifest/catalog identity mismatch");
                foreach (var actor in definition.actors)
                    if (archive.GetEntry("abdata/" + actor.path) == null) throw new Exception("Unresolved native actor bundle");
                if (value.DonorPoseId < 0 || value.categories.Length == 0) throw new Exception("Missing native donor/location definition");
                count++;
            }
        }
        if (count != catalog.Length) throw new Exception("Package/catalog pose count mismatch");
        Console.WriteLine("PASS installed AnimationLoader XML reader and helper catalog: " + count + " native H poses, actor slots, stable identities and bundle references");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run(string path)
    {
        using (var archive = ZipFile.OpenRead(path))
        {
            XDocument doc;
            using (var stream = archive.GetEntry("manifest.xml").Open()) doc = XDocument.Load(stream);
            var root = doc.Root;
            if (root.Element("guid").Value != ZipmodPose.Guid) throw new Exception("Wrong GUID");
            var loader = root.Element("AnimationLoader");
            if (loader.Elements("Animation").Any()) throw new Exception("Accidentally exposed to KK");
            var animation = loader.Element("KoikatsuSunshine").Elements("Animation").Single();
            var serializer = new XmlSerializer(typeof(SwapAnimationInfo));
            SwapAnimationInfo value;
            using (var reader = animation.CreateReader()) value = (SwapAnimationInfo)serializer.Deserialize(reader);
            value.Guid = root.Element("guid").Value;
            if (!ZipmodPose.Matches(value) || value.DonorPoseId != 0 || value.AnimationName != "サマバケ・立ちキス" || value.ExpTaii != -1)
                throw new Exception("Installed loader parsed a different pose");
            if (!value.categories.Select(c => (int)c).SequenceEqual(new[] { 0, 1 })) throw new Exception("Locations differ");
            foreach (string bundle in new[] { value.PathFemale, value.PathMale })
                if (archive.GetEntry("abdata/" + bundle) == null) throw new Exception("Missing zipmod bundle: " + bundle);
            foreach (string field in new[] { "Guid", "StudioId", "Mode", "ControllerFemale", "ControllerMale", "PathFemale" })
            {
                object before = typeof(SwapAnimationInfo).GetField(field).GetValue(value);
                object bad = field == "StudioId" ? (object)(-1) : field == "Mode" ? Enum.ToObject(typeof(SwapAnimationInfo).GetField(field).FieldType, 2) : "wrong";
                typeof(SwapAnimationInfo).GetField(field).SetValue(value, bad);
                if (ZipmodPose.Matches(value)) throw new Exception("Foreign identity accepted: " + field);
                typeof(SwapAnimationInfo).GetField(field).SetValue(value, before);
            }
        }
    }
}
