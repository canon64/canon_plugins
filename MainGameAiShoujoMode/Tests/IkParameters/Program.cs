using System;
using System.Reflection;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Runtime.Serialization;
using MainGameAiShoujoMode.Conversation;
using RootMotion.FinalIK;

static class Program
{
    [DataContract] public sealed class Fixture {
        [DataMember] public float PositionWeight { get; set; }
        [DataMember] public float RotationWeight { get; set; }
        [DataMember] public int Chain { get; set; }
        [DataMember] public int Node { get; set; }
    }
    static void Main()
    {
        Fixture fixture;
        using (var stream = File.OpenRead(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "case.json")))
            fixture = (Fixture)new DataContractJsonSerializer(typeof(Fixture)).ReadObject(stream);
        var effector = new IKEffector { positionWeight = fixture.PositionWeight, rotationWeight = fixture.RotationWeight };
        var index = typeof(IKEffector).GetField("chainIndex", BindingFlags.NonPublic | BindingFlags.Instance);
        var node = typeof(IKEffector).GetField("nodeIndex", BindingFlags.NonPublic | BindingFlags.Instance);
        var restore = KksIkParameterSnapshot.Capture(effector);
        // Simulate native Initiate after the conversation captures an uninitiated solver.
        index.SetValue(effector, fixture.Chain); node.SetValue(effector, fixture.Node);
        effector.positionWeight = default(float); effector.rotationWeight = default(float);
        restore();
        if ((int)index.GetValue(effector) != fixture.Chain || (int)node.GetValue(effector) != fixture.Node)
            throw new Exception("Conversation rollback restored stale native IK indexes.");
        if (effector.positionWeight != fixture.PositionWeight || effector.rotationWeight != fixture.RotationWeight)
            throw new Exception("Conversation motion parameters were not restored.");
        restore();
        if ((int)index.GetValue(effector) != fixture.Chain) throw new Exception("Repeated rollback changed the solver cache.");
        Console.WriteLine("KKS_IK_PARAMETERS_PASS: native cache survives rollback; motion weights restored.");
    }
}
