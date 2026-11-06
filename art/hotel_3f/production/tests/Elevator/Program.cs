using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using MainGameAiShoujoMode;

namespace MainGameAiShoujoMode
{
    // Serialization boundary only; routing logic is the production source.
    public sealed class HotelFloorDefinition
    {
        public string id;
        public object room;
        public HotelPortalDefinition[] portals;
    }
    public sealed class HotelPortalDefinition { public string destination; }
    public sealed class Manifest { public HotelFloorDefinition[] floors; }
}

internal static class Program
{
    private static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static void Main(string[] args)
    {
        int routes = 0;
        foreach (var path in args)
        {
            var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(path), new JsonSerializerOptions { IncludeFields = true });
            var destinations = HotelElevatorRoutes.Destinations(manifest.floors);
            Require(new[] { "lobby", "second", "third" }.All(id => destinations.Any(f => f.id == id)), "A public floor is missing");
            Require(destinations.All(f => f.room == null), "A guest-room door became a public elevator stop");
            foreach (var source in manifest.floors)
            {
                Require(HotelElevatorRoutes.OpensSelector(source, null, manifest.floors) == (source.room == null), "Room exit/elevator distinction failed");
                foreach (var portal in source.portals ?? Array.Empty<HotelPortalDefinition>())
                {
                    var target = manifest.floors.Single(f => f.id == portal.destination);
                    Require(HotelElevatorRoutes.OpensSelector(source, portal, manifest.floors) == (source.room == null && target.room == null), "Door portal was rerouted");
                }
                if (source.room != null) continue;
                foreach (var target in destinations.Where(f => f.id != source.id))
                {
                    Require(!string.IsNullOrWhiteSpace(HotelElevatorRoutes.Label(target)), "Destination is unlabeled");
                    routes++;
                }
            }
            var added = new HotelFloorDefinition { id = "fourth" };
            var expanded = manifest.floors.Append(added).ToArray();
            Require(HotelElevatorRoutes.Destinations(expanded).Contains(added), "New public floors require fixed route edits");
            Require(HotelElevatorRoutes.Label(added).Contains("4F"), "Fourth-floor label missing");
        }
        Require(routes > 0, "No manifests tested");
        Console.WriteLine("HOTEL_ELEVATOR_ROUTES_PASS manifests=" + args.Length + " directedRoutes=" + routes + " roomDoorsPreserved=true futureFloor=true");
    }
}
