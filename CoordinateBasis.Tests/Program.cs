using System;
using System.IO;
using System.Runtime.Serialization.Json;
using CanonPlugins.Shared;
using UnityEngine;
using Composer = MainGameObjectComposer.Plugin;
using Hip = MainGirlHipHijack.Plugin;
using N = System.Numerics;

internal static class Program
{
    private static int _checks;
    private static void Check(bool ok, string name)
    { if (!ok) throw new Exception(name); _checks++; Console.WriteLine("PASS " + name); }
    private static bool Near(Vector3 a, Vector3 b) => N.Vector3.Distance(a.Value, b.Value) < 0.001f;
    private static bool Near(Quaternion a, Quaternion b) => MathF.Abs(N.Quaternion.Dot(a.Value, b.Value)) > 0.99999f;

    private static int Main()
    {
        PresetEditorTests.Run(Check);
        foreach (float yaw in new[] { 0f, 90f, 217f })
        {
            var root = new Transform { position = new Vector3(2, 0.2f, -64), rotation = Quaternion.Euler(new Vector3(0, yaw, 0)), scale = new Vector3(2, 1.5f, 0.5f) };
            var local = new Vector3(0.1f, 0.4f, -0.3f);
            var world = root.TransformPoint(local);
            var rotation = root.rotation * Quaternion.Euler(new Vector3(0, 20, 0));
            var worldRuntimePose = MainGirlHipHijack.Runtime.CharacterRootPose.Capture(root, false, world, rotation);
            var runtimePose = new MainGirlHipHijack.Runtime.CharacterRootPose(root, world, rotation);
            Check(Near(runtimePose.Position, world) && Near(runtimePose.Rotation, rotation), "runtime IK reparent snapshot keeps world pose at capture");
            var runtimeEnd = new MainGirlHipHijack.Runtime.CharacterRootPose(root,
                root.TransformPoint(new Vector3(0.3f, 0.8f, -0.1f)), rotation);
            var saved = RootRelativePose.Capture(root, world, rotation);
            Check(Near(saved.LocalPosition, local) && Near(saved.WorldPosition(root), world), "female-root pose capture and world roundtrip including scale");
            Check(Near(saved.WorldRotation(root), rotation), "rotation roundtrip");
            var serializer = new DataContractJsonSerializer(typeof(RootRelativePose));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, saved); stream.Position = 0;
                var read = (RootRelativePose)serializer.ReadObject(stream);
                Check(Near(read.WorldPosition(root), world) && Near(read.WorldRotation(root), rotation), "HipHijack serializer retains relative position and rotation");
            }
            var composer = new Composer(root);
            var obj = new MainGameObjectComposer.ManagedObjectData { parentKind = "root", localPosition = world, localEulerAngles = rotation.eulerAngles };
            composer.Capture(obj);
            Check(obj.femaleRootPose != null && Near(obj.worldPosition, world), "Composer stores both coordinates from one snapshot");
            root.position = new Vector3(100, 8, 400);
            root.rotation = Quaternion.Euler(new Vector3(0, 44, 0));
            Check(Near(worldRuntimePose.Position, world) && Near(worldRuntimePose.Rotation, rotation),
                "world-mode runtime transition and hold do not follow female root movement");
            Check(Near(runtimePose.Position, root.TransformPoint(local)) && Near(runtimePose.Rotation, root.rotation * Quaternion.Euler(new Vector3(0, 20, 0))),
                "runtime transition, hold and gauge snapshots follow root translation and rotation");
            Check(Near(runtimeEnd.Position - runtimePose.Position,
                root.TransformPoint(new Vector3(0.3f, 0.8f, -0.1f)) - root.TransformPoint(local)),
                "distinct runtime endpoints retain their difference after moving rooms");
            Check(Near(new Vector3(N.Vector3.Lerp(runtimePose.Position.Value, runtimeEnd.Position.Value, 0.5f)),
                root.TransformPoint(new Vector3(0.2f, 0.6f, -0.2f))),
                "gauge and transition interpolation stays attached after root moves");
            root.position = new Vector3(-300, 12, 99);
            Check(Near(runtimePose.Position, root.TransformPoint(local)), "repeated H-location moves do not accumulate correction");
            root.position = new Vector3(100, 8, 400);
            composer.Resolve(obj, true);
            Check(obj.parentKind == "external" && obj.parentRefId == "char:root" && Near(obj.localPosition, local), "Composer female-root setting selects relative parent and pose in another room");
            composer.Resolve(obj, false);
            Check(obj.parentKind == "root" && Near(obj.localPosition, world), "Composer world setting restores original world placement");
            composer.Resolve(obj, true); composer.Resolve(obj, false);
            Check(Near(obj.localPosition, world) && Near(obj.femaleRootPose.LocalPosition, local), "changing basis never accumulates offsets or changes stored pair");
            var hip = new Hip(root);
            var entry = new Hip.PosePresetEntryRuntime { proxyPosition = world, proxyRotation = rotation, femaleRootPose = saved };
            hip.Resolve(entry, true, out var p, out var q);
            Check(Near(p, root.TransformPoint(local)) && Near(q, saved.WorldRotation(root)), "HipHijack female-root setting restores relative pose in another room");
            hip.Resolve(entry, false, out p, out q);
            Check(Near(p, world) && Near(q, rotation), "HipHijack world setting restores original pose");
            entry.femaleRootPose = null;
            hip.Resolve(entry, true, out p, out q);
            Check(Near(p, world) && Near(q, rotation), "legacy HipHijack world pose is preserved without inventing a root");
            var legacy = new MainGameObjectComposer.ManagedObjectData { parentKind = "root", localPosition = world };
            composer.Resolve(legacy, true);
            Check(legacy.parentKind == "root" && Near(legacy.localPosition, world), "legacy Composer parent and world pose stay unchanged");
            var child = new MainGameObjectComposer.ManagedObjectData { parentKind = "managed", parentRefId = "driver", localPosition = local };
            composer.Capture(child); composer.Resolve(child, true);
            Check(child.parentKind == "managed" && child.parentRefId == "driver" && Near(child.localPosition, local), "managed orbit child is not transformed twice");
            var bone = new MainGameObjectComposer.ManagedObjectData { parentKind = "external", parentRefId = "fk:hips", localPosition = local };
            composer.Capture(bone); composer.Resolve(bone, false);
            Check(bone.parentRefId == "fk:hips" && Near(bone.localPosition, local), "explicit bone-follow parenting is preserved");
            var noActor = new Composer(null);
            noActor.Capture(legacy);
            Check(legacy.femaleRootPose == null, "startup cannot generate coordinates without a source actor");
        }
        Console.WriteLine("Passed " + _checks + " coordinate/serialization boundary checks; no live Unity test.");
        return 0;
    }
}
