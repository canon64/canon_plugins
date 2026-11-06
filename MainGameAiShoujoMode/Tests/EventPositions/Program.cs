using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using MainGameAiShoujoMode.Conversation;
using MapEnvironmentRuntime;

internal static class Program
{
    [DataContract] private sealed class Fixture
    {
        [DataMember] public EventPositionSnapshot snapshot;
        [DataMember] public float distance, horizontalDistance, heightDelta;
    }
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); Console.WriteLine("PASS " + message); }
    private static void Main(string[] args)
    {
        Console.WriteLine("EVENT_POSITION_CHECKS_START");
        var fixture = JsonCodec.FromJson<Fixture>(File.ReadAllText(args.First()));
        var snapshot = fixture.snapshot;
        snapshot.maleToFemale = EventPositionSnapshot.Relation.Between(snapshot.player, snapshot.girl);
        var json = JsonCodec.ToJson(snapshot);
        var copy = JsonCodec.FromJson<EventPositionSnapshot>(json);
        Check(copy.player.world.Equals(snapshot.player.world), "male XYZ survives real JSON codec");
        Check(copy.girl.islandSource.Equals(snapshot.girl.islandSource) && copy.girl.world.Equals(snapshot.girl.world), "female world and island XYZ survive");
        Check(copy.selectedPoint.world.Equals(snapshot.selectedPoint.world) && copy.view.islandSource.Equals(snapshot.view.islandSource), "selected point and view survive");
        Check(copy.maleToFemale.heightDelta == fixture.heightDelta && copy.maleToFemale.distance == fixture.distance
            && copy.maleToFemale.horizontalDistance == fixture.horizontalDistance, "signed displacement and horizontal/3D distances are correct");
        Check(copy.selection.candidates.First().sourcePosition.Equals(snapshot.selection.candidates.First().sourcePosition)
            && copy.selection.candidates.First().result == "selected"
            && copy.selection.candidates.First().categories.SequenceEqual(snapshot.selection.candidates.First().categories), "candidate coordinates categories and reason survive");
        Check(copy.placedFurniture.First().slot.world.Equals(snapshot.placedFurniture.First().slot.world), "placed bed slot coordinates survive");
        Check(EventPositionSnapshot.Relation.Between(null, snapshot.player) == null, "missing actor is explicit null");
        Console.WriteLine("EVENT_POSITION_CHECKS_PASS");
    }
}
