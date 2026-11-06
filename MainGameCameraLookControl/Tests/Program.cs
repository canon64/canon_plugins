using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using MainGameCameraLookControl;
using UnityEngine;

int assertions = 0;
void Check(bool value, string label)
{
    assertions++;
    if (!value) throw new Exception(label);
}
void Near(float actual, float expected, string label) => Check(Math.Abs(actual - expected) < 0.0001f, label + $": {actual} vs {expected}");
NeckLimitParameter Param(float up) => new NeckLimitParameter
{
    name = "bone", upBendingAngle = up, downBendingAngle = 30, minBendingAngle = -45, maxBendingAngle = 45
};
NeckLimitParameter[] Scratch() => new[] { Param(0), Param(0) };

Console.WriteLine("Camera-look policy, native-state lease, roll geometry and installed ABI checks starting");
var policy = new LookPolicy { Neck = new BonePolicy { Up = 10 }, Head = new BonePolicy { Up = 20 } };
Near(LookPolicy.Upper(-20, 10), -30, "positive UI addition increases upward magnitude");
Near(LookPolicy.Upper(-20, -10), -10, "negative addition restricts upward range");
Near(LookPolicy.Upper(-5, -20), 0, "up range cannot invert sign");
Near(LookPolicy.Upper(-95, 20), -100, "up hard limit");
Near(LookPolicy.Lower(20, 10), 30, "down/right expansion");
Near(LookPolicy.Lower(5, -20), 0, "down cannot invert sign");
Near(LookPolicy.Lower(95, 20), 100, "down hard limit");
Near(LookPolicy.Upper(-150, 0), -150, "zero correction preserves even nonstandard native values");
Near(policy.VerticalField(80, -60), 110, "upper recognition field expands");
Near(policy.VerticalField(80, 60), 80, "lower recognition field remains native");
policy.LeftField = 15;
Near(policy.HorizontalField(100, -60), 115, "left recognition field expands separately");
Near(policy.HorizontalField(100, 60), 100, "right recognition field remains native");
policy.UpField = 180;
Near(policy.VerticalField(80, -60), 180, "recognition range hard limit");
policy.PitchGain = 0.5f; policy.UpBias = 10;
Near(policy.Pitch(-40), -30, "pitch gain and upward bias use native sign");
policy.YawGain = 0.5f; policy.RightBias = 5;
Near(policy.Yaw(30), 20, "yaw gain and right bias");
Near(LookPolicy.Sanitize(float.NaN, 1, 0, 2), 1, "NaN setting uses its own default");
Near(LookPolicy.Sanitize(float.PositiveInfinity, 1, 0, 2), 1, "infinite setting uses its own default");
Near(LookPolicy.Sanitize(5, 1, 0, 2), 2, "out-of-range setting is bounded");
Near(LookPolicy.Roll(new BonePolicy { Roll = 5, RollFromYaw = 0.25f }, 30), 12.5f, "roll offset and yaw link");
Near(LookPolicy.Roll(new BonePolicy { Roll = 40, RollFromYaw = 1 }, 60), 60, "roll hard limit");
Near(LookPolicy.Roll(new BonePolicy { Roll = -40, RollFromYaw = 1 }, -60), -60, "negative roll hard limit");

policy = new LookPolicy { Neck = new BonePolicy { Up = 10 }, Head = new BonePolicy { Up = 20 }, Speed = 2 };
var originals = new[] { Param(-20), Param(-30) };
var state = new NeckTypeStateVer2 { aParam = originals, hAngleLimit = 100, vAngleLimit = 80, leapSpeed = 2.5f };
try
{
    using var lease = new NativeParameterLease(state, policy, -40, 0, Scratch());
    Near(state.aParam[0].upBendingAngle, -30, "neck cap applies to first bone");
    Near(state.aParam[1].upBendingAngle, -50, "head cap applies to last bone");
    Near(originals[0].upBendingAngle, -20, "original parameter object is untouched");
    Near(state.aParam[0].downBendingAngle, 30, "down cap is unchanged");
    Near(state.leapSpeed, 5, "native interpolation speed is scaled");
    Check(!ReferenceEquals(originals, state.aParam), "call uses private scratch array");
    state.isLimitBreakBackup = true;
    throw new InvalidOperationException("simulated native error");
}
catch (InvalidOperationException) { }
Check(ReferenceEquals(state.aParam, originals), "exception restores original array identity");
Near(state.hAngleLimit, 100, "horizontal gate restored");
Near(state.vAngleLimit, 80, "vertical gate restored");
Near(state.leapSpeed, 2.5f, "native speed restored");
Check(state.isLimitBreakBackup, "game-owned hysteresis output is preserved");
originals[0].upBendingAngle = -35; // New pose supplied by the game.
using (var lease = new NativeParameterLease(state, policy, -40, 0, Scratch()))
    Near(state.aParam[0].upBendingAngle, -45, "new pose is the baseline, without accumulated extensions");
var other = new NeckTypeStateVer2 { aParam = new[] { Param(-10), Param(-15) }, hAngleLimit = 80, vAngleLimit = 70, leapSpeed = 1 };
using (var outer = new NativeParameterLease(state, policy, -40, 0, Scratch()))
{
    var outerArray = state.aParam;
    using (var inner = new NativeParameterLease(other, policy, -40, 0, Scratch()))
        Near(other.aParam[0].upBendingAngle, -20, "second actor has independent native limits");
    Check(ReferenceEquals(state.aParam, outerArray), "second actor does not end first actor's lease");
}
Check(ReferenceEquals(state.aParam, originals), "normal return restores first actor");
using (var outer = new NativeParameterLease(state, policy, -40, 0, Scratch()))
{
    var outerArray = state.aParam;
    using (var nested = new NativeParameterLease(state, new LookPolicy { UpField = 0 }, 40, 0, Scratch())) { }
    Check(ReferenceEquals(state.aParam, outerArray), "nested lease restores its immediate parent");
}

var root = new Transform { localRotation = Quaternion.AngleAxis(75, new Vector3(0, 0, 1)) };
var neck = new Transform { parent = root, localRotation = Quaternion.AngleAxis(-15, new Vector3(1, 0, 0)) };
var head = new Transform { parent = neck, localRotation = Quaternion.AngleAxis(25, new Vector3(0, 1, 0)) };
var nativeNeck = neck.localRotation;
var nativeHead = head.localRotation;
var nativeForward = head.rotation.Forward;
var layer = new RollLayer(new[] { neck, head });
Quaternion first = Quaternion.identity;
for (int i = 0; i < 100; i++)
{
    layer.Apply(new[] { 12f, 8f });
    Check(System.Numerics.Vector3.Distance(head.rotation.Forward, nativeForward) < 0.0001f, "roll preserves camera aim on a rotated body");
    if (i == 0) first = head.rotation;
    else Check(Quaternion.Angle(first, head.rotation) < 0.06f, "repeated calculation does not accumulate roll");
}
layer.Remove();
Check(Quaternion.Angle(nativeNeck, neck.localRotation) < 0.06f, "release restores native neck local rotation");
Check(Quaternion.Angle(nativeHead, head.localRotation) < 0.06f, "release restores native head local rotation");
layer.Apply(new[] { 12f, 8f });
var animationHead = Quaternion.AngleAxis(45, new Vector3(1, 0, 0));
head.localRotation = animationHead;
layer.Remove();
Check(Quaternion.Angle(animationHead, head.localRotation) < 0.06f, "cleanup does not overwrite an animation's new rotation");
layer.Apply(new[] { 0f, 0f });
Check(Quaternion.Angle(animationHead, head.localRotation) < 0.06f, "zero Z correction leaves pose untouched");

var selector = new CameraTargets();
Check(selector.IsCamera(new Transform { camera = new Camera() }), "explicit render camera is a camera target");
Check(!selector.IsCamera(new Transform()), "partner or dummy target is not a camera just because it is a transform");
Check(!selector.IsCamera(null), "missing target is not camera gaze");

foreach (string directory in new[] { "KoikatsuSunshine_Data", "KoikatsuSunshine_VR_Data", "CharaStudio_Data" })
{
    string path = Path.Combine("F:\\kks", directory, "Managed", "Assembly-CSharp.dll");
    using var stream = File.OpenRead(path);
    using var pe = new PEReader(stream);
    var reader = pe.GetMetadataReader();
    var requiredTypes = new[] { "NeckLookControllerVer2", "NeckLookCalcVer2", "NeckTypeStateVer2", "NeckLimitParameter" };
    var types = reader.TypeDefinitions.Select(h => reader.GetTypeDefinition(h)).Where(t => t.GetDeclaringType().IsNil &&
        reader.GetString(t.Namespace).Length == 0 && requiredTypes.Contains(reader.GetString(t.Name)))
        .ToDictionary(t => reader.GetString(t.Name), t => t, StringComparer.Ordinal);
    foreach (string name in new[] { "NeckLookControllerVer2", "NeckLookCalcVer2", "NeckTypeStateVer2", "NeckLimitParameter" })
        Check(types.ContainsKey(name), directory + " native type " + name);
    foreach (var contract in new[]
    {
        (Type: "NeckLookControllerVer2", Method: "LateUpdate", Parameters: Array.Empty<string>()),
        (Type: "NeckLookControllerVer2", Method: "ForceLateUpdate", Parameters: Array.Empty<string>()),
        (Type: "NeckLookCalcVer2", Method: "NeckUpdateCalc", Parameters: new[] { "target", "ptnNo", "_isUseBackUpPos" }),
        (Type: "NeckLookCalcVer2", Method: "GetAngleToTarget", Parameters: new[] { "_targetPosition", "_bone", "_weight" })
    })
    {
        var methods = types[contract.Type].GetMethods().Select(h => reader.GetMethodDefinition(h))
            .Where(m => reader.GetString(m.Name) == contract.Method).ToArray();
        Check(methods.Length == 1, directory + " unambiguous hook " + contract.Method);
        var parameters = methods[0].GetParameters().Select(h => reader.GetParameter(h)).Where(p => p.SequenceNumber > 0)
            .OrderBy(p => p.SequenceNumber).Select(p => reader.GetString(p.Name)).ToArray();
        Check(parameters.SequenceEqual(contract.Parameters), directory + " Harmony parameter binding " + contract.Method);
    }
    foreach (var contract in new[]
    {
        (Type: "NeckLookCalcVer2", Fields: new[] { "initEnd", "aBones", "neckTypeStates", "skipCalc", "isEnabled" }),
        (Type: "NeckTypeStateVer2", Fields: new[] { "isLimitBreakBackup", "aParam", "hAngleLimit", "vAngleLimit", "leapSpeed" })
    })
    {
        var fields = types[contract.Type].GetFields().Select(h => reader.GetString(reader.GetFieldDefinition(h).Name)).ToHashSet();
        foreach (string name in contract.Fields) Check(fields.Contains(name), directory + " native state field " + name);
    }
    Console.WriteLine("Installed ABI: " + directory + " PASS");
}
Console.WriteLine($"Camera-look checks: PASS ({assertions} assertions)");
