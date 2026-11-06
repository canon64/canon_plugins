using System;
using System.IO;
using System.Linq;
using MainGameBlankMapAdd;

class Program
{
    static int checks;
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; }
    static MapWarpPoint Capture(float offset = 1) => new MapWarpPoint {
        X = 3, Y = 4, Z = 5, Scale = 1,
        Placement = new float[] { offset, 2, 3, 4, 5, 6 }, PlacementActorCenter = true
    };
    static void Main()
    {
        Console.WriteLine("Testing persistent default map presets...");
        var original = new MapWarpDocument();
        original.Points.Add(new MapWarpPoint { Id = "existing", MapKey = "map-a", Name = "窓際", Scale = 1 });
        var document = MapWarpOrigin.CreateIfMissing(original, "map-a", () => Capture());
        Check(original.Points.Count == 1, "registration does not mutate the old document before saving");
        Check(document.Points.Count == 2 && document.Points[0].IsOrigin, "origin inserted first without losing existing landmarks");
        var origin = MapWarpOrigin.Find(document, "map-a");
        Check(origin.Name == "元の場所" && origin.Placement.SequenceEqual(new float[] { 1, 2, 3, 4, 5, 6 }), "full default placement captured");
        Check(origin.PlacementActorCenter, "rotation center mode captured");
        Check(MapWarpOrigin.CreateIfMissing(document, "map-a", () => throw new Exception("recaptured")) == null, "normal navigation cannot overwrite origin");
        var copy = document.Clone(); copy.Points[0].Placement[0] = 77;
        Check(origin.Placement[0] == 1, "failed candidate edits cannot alter the saved origin");
        document = MapWarpOrigin.CreateIfMissing(document, "map-b", () => Capture(9));
        Check(MapWarpOrigin.Find(document, "map-a").Placement[0] == 1 && MapWarpOrigin.Find(document, "map-b").Placement[0] == 9, "maps have independent origins");
        var hotel = Capture(); hotel.Placement = new float[] { 1, 2, 3, 4, 5, 6, .7f }; hotel.Scale = .7f;
        document = MapWarpOrigin.CreateIfMissing(document, "hotel", () => hotel);
        string path = Path.Combine(AppContext.BaseDirectory, "origin-test.json");
        try
        {
            MapWarpPointStore.Save(path, document);
            var loaded = MapWarpPointStore.Load(path);
            string photoDirectory = Path.Combine(AppContext.BaseDirectory, Guid.NewGuid().ToString("N"));
            try
            {
                var originalBytes = File.ReadAllBytes(path);
                var photoOrigin = MapWarpOrigin.Find(loaded, "map-a");
                var firstPhoto = new byte[] { 1, 2, 3 };
                MapWarpOrigin.SavePhoto(photoDirectory, photoOrigin, firstPhoto);
                string photoPath = MapWarpOrigin.PhotoPath(photoDirectory, photoOrigin);
                Check(File.ReadAllBytes(photoPath).SequenceEqual(firstPhoto), "origin photo saved separately from placement");
                MapWarpOrigin.SavePhoto(photoDirectory, photoOrigin, firstPhoto.Reverse().ToArray());
                Check(File.ReadAllBytes(path).SequenceEqual(originalBytes), "retaking origin screenshot leaves entire placement document byte-identical");
                Check(photoOrigin.X == origin.X && photoOrigin.Placement.SequenceEqual(origin.Placement), "screenshot operation never changes origin coordinates or map placement");
                using (var locked = new FileStream(photoPath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    bool failed = false;
                    try { MapWarpOrigin.SavePhoto(photoDirectory, photoOrigin, firstPhoto); } catch (IOException) { failed = true; }
                    Check(failed, "photo replacement failure is reported");
                }
                Check(File.ReadAllBytes(photoPath).SequenceEqual(firstPhoto.Reverse()) && !File.Exists(photoPath + ".tmp"), "failed photo replacement preserves previous image and removes partial file");
                bool invalidPhoto = false;
                try { MapWarpOrigin.PhotoPath(photoDirectory, loaded.Points.Single(p => p.Id == "existing")); } catch (InvalidDataException) { invalidPhoto = true; }
                Check(invalidPhoto, "origin-only photo storage rejects ordinary or unsafe IDs");
            }
            finally
            {
                foreach (string photo in Directory.GetFiles(photoDirectory)) File.Delete(photo);
                Directory.Delete(photoDirectory);
            }
            Check(loaded.Points.Count == 4 && loaded.Points.Any(p => p.Id == "existing"), "reload retains all existing landmarks");
            Check(MapWarpOrigin.Find(loaded, "map-a").Placement.SequenceEqual(origin.Placement), "origin survives file reload exactly");
            Check(MapWarpOrigin.Find(loaded, "map-a").PlacementActorCenter, "rotation center mode survives reload");
            Check(MapWarpOrigin.Find(loaded, "hotel").Placement[6] == .7f, "hotel scale survives reload");
            Check(MapWarpOrigin.CreateIfMissing(loaded, "map-a", () => Capture(99)) == null, "restart cannot recapture the last warped location as origin");
            var invalid = loaded.Clone(); invalid.Points.Add(MapWarpOrigin.Find(loaded, "map-a").Clone()); invalid.Points.Last().Id = "duplicate-origin";
            bool rejected = false; try { MapWarpPointStore.Save(path, invalid); } catch (InvalidDataException) { rejected = true; }
            Check(rejected && MapWarpPointStore.Load(path).Points.Count == 4, "duplicate origins rejected without damaging disk data");
            invalid = loaded.Clone(); MapWarpOrigin.Find(invalid, "map-a").Placement[0] = float.NaN;
            rejected = false; try { MapWarpPointStore.Save(path, invalid); } catch (InvalidDataException) { rejected = true; }
            Check(rejected && MapWarpOrigin.Find(MapWarpPointStore.Load(path), "map-a").Placement[0] == 1, "invalid origin rejected without replacing saved placement");
        }
        finally { if (File.Exists(path)) File.Delete(path); if (File.Exists(path + ".bak")) File.Delete(path + ".bak"); }
        Console.WriteLine("PASS " + checks + " default map preset checks");
    }
}
