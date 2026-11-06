using System.Numerics;
using MainGameVrNeckChain;

int assertions = 0;
void Check(bool condition, string message)
{
    assertions++;
    if (!condition) throw new Exception(message);
}
void Near(float actual, float expected, string message) => Check(Math.Abs(actual - expected) < 0.00001f, message);

Console.WriteLine("Shoulder pull state checks starting");
var pull = new ShoulderPullState();
Near(pull.Sample(0.8f, 1f, 0.5f), 0f, "slack movement");
Check(!pull.Active, "slack must not capture");
Near(pull.Sample(1f, 1f, 0.5f), 0f, "taut starts without movement");
Check(pull.Active, "capture at full length");
int session = pull.Session;
Near(pull.Sample(1.2f, 1f, 0.5f), 0.2f, "20 cm excess");
Near(pull.Sample(1.7f, 1f, 0.5f), 0.5f, "50 cm cap");
Near(pull.Sample(1.7f, 1f, 0.15f), 0.15f, "configured smaller cap");
Near(pull.Sample(1.7f, 1f, 5f), 0.5f, "hard cap even for invalid high maximum");
Near(pull.Sample(1.1f, 1f, 0.5f), 0.1f, "return reduces displacement");
Near(pull.Sample(0.998f, 1f, 0.5f), 0f, "zero displacement in release deadband");
Check(pull.Active && pull.Session == session, "deadband must retain captured pose");
Near(pull.Sample(0.995f, 1f, 0.5f), 0f, "exact release boundary");
Check(pull.Active, "release threshold is strictly below minus 5 mm");
pull.Sample(0.994f, 1f, 0.5f);
Check(!pull.Active, "return releases capture");
pull.Sample(1.2f, 1f, 0.5f);
Check(pull.Active && pull.Session != session, "next pull captures a fresh session");
pull.CancelUntilSlack();
Check(!pull.Active && pull.WaitingForSlack, "posture change cancels hold");
pull.Sample(1.7f, 1f, 0.5f);
Check(!pull.Active, "posture cancellation must not recapture while taut");
pull.Sample(0.99f, 1f, 0.5f);
Check(!pull.WaitingForSlack && !pull.Active, "slack rearms after posture change");
pull.Sample(1f, 1f, 0.5f);
Check(pull.Active, "new pull after posture change");
pull.Reset();
Check(!pull.Active && !pull.WaitingForSlack, "release/detach/removal clears state");
pull.Sample(1.2f, 1f, 0f);
Check(!pull.Active, "zero setting disables");
pull.Sample(1f, 1f, 0.5f);
pull.Sample(1.3f, 1f, 0f);
Check(!pull.Active, "changing setting to zero releases");
pull.Sample(float.NaN, 1f, 0.5f);
Check(!pull.Active, "NaN distance cannot start a hold");
pull.Sample(2f, float.PositiveInfinity, 0.5f);
Check(!pull.Active, "invalid chain length cannot start a hold");

// Same anchored distance, different direction: no accumulated translation or rotation.
var anchor = new Vector3(0.2f, 1f, -0.4f);
var left = new Vector3(-0.2f, 1.3f, 0f);
var right = new Vector3(0.2f, 1.3f, 0f);
pull.Reset();
foreach (var direction in new[] { Vector3.UnitX, Vector3.UnitY, -Vector3.UnitZ, Vector3.Normalize(Vector3.One) })
{
    Vector3 tip = anchor + direction * 1.2f;
    Vector3 delta = tip - anchor;
    pull.SampleDelta(delta.X, delta.Y, delta.Z, 1f, 0.5f, out float x, out float y, out float z);
    Vector3 offset = new Vector3(x, y, z);
    Near(offset.Length(), 0.2f, "direction change preserves displacement magnitude");
    Near(Vector3.Distance((right + offset) - (left + offset), right - left), 0f, "both shoulders preserve spacing");
    Near(Vector3.Dot(Vector3.Normalize(offset), direction), 1f, "offset follows pull direction");
}
pull.SampleDelta(0f, 0f, 0f, 1f, 0.5f, out float zeroX, out float zeroY, out float zeroZ);
Check(!pull.Active && zeroX == 0f && zeroY == 0f && zeroZ == 0f, "coincident hand and anchor releases without NaN");
pull.SampleDelta(float.PositiveInfinity, 0f, 0f, 1f, 0.5f, out zeroX, out zeroY, out zeroZ);
Check(!pull.Active && zeroX == 0f && zeroY == 0f && zeroZ == 0f, "invalid hand position releases without propagating NaN");
Console.WriteLine($"Shoulder pull state: PASS ({assertions} assertions)");
ChainSimulationChecks.Run();
ChainRestoreChecks.Run();
