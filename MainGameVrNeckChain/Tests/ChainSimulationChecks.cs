using MainGameVrNeckChain;
using V = UnityEngine.Vector3;

internal static class ChainSimulationChecks
{
    private static int _checks;
    private static void Check(bool value, string message)
    {
        _checks++;
        if (!value) throw new Exception(message);
    }

    internal static int Run()
    {
        const int count = 24;
        var initial = new V[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / (count - 1);
            initial[i] = new V(t * 0.8f, -0.2f * (float)Math.Sin(Math.PI * t), 0f);
        }
        var lengths = new float[count - 1];
        for (int i = 0; i < lengths.Length; i++) lengths[i] = (initial[i + 1] - initial[i]).magnitude;
        float total = lengths.Sum();

        var points = (V[])initial.Clone();
        var previous = (V[])initial.Clone();
        V shift = new V(0.3f, 0.2f, -0.1f);
        ChainSimulation.Step(points, previous, lengths, initial[0] + shift, initial[count - 1] + shift,
            true, true, 0f, 0f);
        for (int i = 0; i < count; i++)
        {
            Check((points[i] - initial[i] - shift).magnitude < 0.00001f, "equal endpoint movement must translate the whole rope");
            Check((points[i] - previous[i]).magnitude < 0.00001f, "endpoint transport must not create artificial velocity");
        }

        // An endpoint must not pull all spare links into the root. Test actual
        // production integration and constraints over a moving-collar sequence.
        points = (V[])initial.Clone();
        previous = (V[])initial.Clone();
        V root = initial[0];
        V tip = initial[count - 1];
        for (int frame = 0; frame < 180; frame++)
        {
            root = new V(0.2f * Math.Min(1f, frame / 60f), 0f, 0f);
            ChainSimulation.Step(points, previous, lengths, root, tip, true, true, 1f / 60f, 6f);
        }
        Check((points[0] - root).magnitude < 0.00001f, "root follows final collar position");
        Check((points[count - 1] - tip).magnitude < 0.00001f, "reachable tip stays at the hand");
        float actualTotal = 0f;
        int lowest = 0;
        for (int i = 0; i < lengths.Length; i++)
        {
            float actual = (points[i + 1] - points[i]).magnitude;
            actualTotal += actual;
            Check(Math.Abs(actual - lengths[i]) < 0.002f, "links must retain length after root approaches tip");
            if (points[i].y < points[lowest].y) lowest = i;
        }
        Check(Math.Abs(actualTotal - total) < 0.01f, "slack must not shorten total chain length");
        Check(lowest > 4 && lowest < count - 5, "slack must hang between endpoints instead of bunching at root");

        // A translating character and hand must behave identically to a stationary
        // simulation in translated coordinates, even after many gravity steps.
        var stationary = (V[])initial.Clone();
        var stationaryPrevious = (V[])initial.Clone();
        points = (V[])initial.Clone();
        previous = (V[])initial.Clone();
        for (int frame = 1; frame <= 90; frame++)
        {
            shift = new V(0.003f * frame, 0.002f * frame, -0.001f * frame);
            ChainSimulation.Step(stationary, stationaryPrevious, lengths, initial[0], initial[count - 1], true, true, 1f / 60f, 6f);
            ChainSimulation.Step(points, previous, lengths, initial[0] + shift, initial[count - 1] + shift, true, true, 1f / 60f, 6f);
        }
        for (int i = 0; i < count; i++)
            Check((points[i] - stationary[i] - shift).magnitude < 0.0001f, "repeated movement must preserve shape instead of reeling into root");

        root = new V(0.1f, 0.3f, -0.2f);
        tip = root + new V(0.3f, 0.4f, 2f);
        ChainSimulation.Step(points, previous, lengths, root, tip, true, true, 1f / 60f, 6f);
        V expectedEnd = root + (tip - root).normalized * total;
        Check((points[count - 1] - expectedEnd).magnitude < 0.00001f, "unreachable tip clamps from the moved root, not the old root");
        for (int i = 0; i < lengths.Length; i++)
            Check(Math.Abs((points[i + 1] - points[i]).magnitude - lengths[i]) < 0.00001f, "taut chain preserves each link length");

        for (int frame = 0; frame < 30; frame++)
            ChainSimulation.Step(points, previous, lengths, root, tip, false, false, 1f / 60f, 6f);
        foreach (V p in points)
            Check(!float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsNaN(p.z), "release remains finite");

        // The installed prefab has 23 intervals of 4 cm. Exercise all directions
        // at the user's configured overall scale, starting from a downward chain.
        // Moving the collar and hand together must translate the visible span,
        // never leave the endpoint measured from an old collar position.
        float interval = 0.04f * 0.6848739f;
        float[] realLengths = Enumerable.Repeat(interval, count - 1).ToArray();
        float realTotal = realLengths.Sum();
        V anchor = new V(0.7f, 1.4f, -0.2f);
        foreach (V direction in new[] { V.right, -V.right, V.up, -V.up, V.forward, -V.forward,
            new V(1f, 1f, 1f).normalized, new V(-1f, 1f, -1f).normalized })
        {
            points = Enumerable.Range(0, count).Select(i => anchor + V.down * (i * interval)).ToArray();
            previous = (V[])points.Clone();
            foreach (float pull in new[] { 0f, 0.1f, 0.3f, 0.5f, 0.2f })
            {
                root = anchor + direction * pull;
                tip = anchor + direction * (realTotal + pull);
                ChainSimulation.Step(points, previous, realLengths, root, tip, true, true, 1f / 60f, 6f);
                for (int i = 0; i < count; i++)
                    Check((points[i] - root - direction * (interval * i)).magnitude < 0.00001f,
                        "every axis must carry every link with the moved collar without reeling");
                Check((points[count - 1] - tip).magnitude < 0.00001f,
                    "tip must reach the hand for every axis within shoulder travel limit");
            }
        }
        // A released handle is an absolute world anchor, including when the
        // collar later moves beyond the available length.
        points = (V[])initial.Clone(); previous = (V[])points.Clone();
        V releasedHandle = initial[count - 1];
        for (int frame = 0; frame < 180; frame++)
        {
            root = initial[0] + new V(-frame * .01f, frame * .003f, 0);
            ChainSimulation.Step(points, previous, lengths, root, releasedHandle,
                true, true, 1f / 60f, 6f, preservePinnedTip: true);
            Check((points[count - 1] - releasedHandle).magnitude < .00001f,
                "released handle drifted when collar moved");
            Check((previous[count - 1] - releasedHandle).magnitude < .00001f,
                "released handle retained falling velocity");
        }
        // Picking it up again makes the endpoint follow a fresh hand target.
        V pickedUp = releasedHandle + new V(.1f, .2f, .3f);
        ChainSimulation.Step(points, previous, lengths, pickedUp + V.down * .1f, pickedUp,
            true, true, 1f / 60f, 6f);
        Check((points[count - 1] - pickedUp).magnitude < .00001f, "grabbed handle retained its old anchor");
        Console.WriteLine($"Chain geometry: PASS ({_checks} assertions)");
        return _checks;
    }
}
