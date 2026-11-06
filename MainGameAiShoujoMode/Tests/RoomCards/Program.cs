using System;
using System.IO;
using System.Linq;
using MainGameAiShoujoMode;

internal static class Program
{
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Main(string[] args)
    {
        string directory = Path.Combine(args.Single(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "rooms.json"), card = Path.Combine(directory, "sample.png");
            var store = new HotelRoomCards(path);
            Require(!store.Load().rooms.Any() && !File.Exists(path), "missing assignment must not write anything");
            store.Set("201", card, "一人目"); store.Set("202", card, "二人目");
            var copy = new HotelRoomCards(path).Load();
            Require(copy.rooms.Select(r => r.room).OrderBy(r => r).SequenceEqual(new[] { "201", "202" }) && copy.rooms.Single(r => r.room == "201").name == "一人目", "restart round trip");
            store.Set("201", card, "変更");
            Require(store.Load().rooms.Single(r => r.room == "202").name == "二人目", "replacement touched another room");
            store.Set("201", null, null);
            Require(store.Load().rooms.Single().room == "202", "clear failed");
            Require(File.Exists(path + ".bak") && !File.Exists(path + ".tmp"), "atomic replacement backup");
            Require(!File.Exists(card) && store.Load().rooms.Any(), "missing card must preserve registration for repair");
            store.Set("reception", card, "受付嬢");
            Require(store.Load().rooms.Single(r => r.room == "reception").name == "受付嬢" && store.Load().rooms.Any(r => r.room == "202"), "reception assignment touched guest rooms");
            bool failed = false;
            try { store.Set("999", card, "invalid"); } catch (ArgumentException) { failed = true; }
            Require(failed, "unknown room accepted");
            File.WriteAllText(path, "{broken"); string broken = File.ReadAllText(path);
            failed = false; try { store.Set("203", card, "x"); } catch { failed = true; }
            Require(failed && File.ReadAllText(path) == broken, "corrupt save was overwritten");
            File.WriteAllText(path, "{\"version\":\"v2\",\"rooms\":[]}");
            failed = false; try { store.Load(); } catch (InvalidDataException) { failed = true; }
            Require(failed, "unknown version accepted");
            File.WriteAllText(path, "{\"version\":\"v1\",\"rooms\":[{\"room\":\"201\",\"card\":\"F:/a.png\"},{\"room\":\"201\",\"card\":\"F:/b.png\"}]}");
            failed = false; try { store.Load(); } catch (InvalidDataException) { failed = true; }
            Require(failed, "duplicate room accepted");
            StateChecks.Run(directory);
            Console.WriteLine("HOTEL_ROOM_CARDS_PASS: empty, restart, replace, clear, atomic backup, missing card, unknown room, corrupt data, version, duplicate");
        }
        finally { Directory.Delete(directory, true); }
    }
}
