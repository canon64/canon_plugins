using MainGameVrNeckChain;
using V = UnityEngine.Vector3;
using Q = UnityEngine.Quaternion;

internal static class ChainRestoreChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool condition, string message)
        {
            ++checks;
            if (!condition) throw new Exception(message);
        }
        // 90 degrees about Z, nonuniform collar scale, translated hotel room.
        var rotation = new Q(0f, 0f, .70710678f, .70710678f);
        var position = new V(3f, 2f, 1f);
        var scale = new V(2f, 3f, .5f);
        var local = new V(.1f, -.2f, .3f);
        var world = position + rotation * V.Scale(local, scale);
        Check((ChainRestoreGeometry.ToLocal(world, position, rotation, scale) - local).magnitude < .00001f,
            "saved collar-local root survives room rotation and nonuniform scale");
        Check(!ChainRestoreGeometry.Finite(new V(float.NaN, 0, 0)) &&
              !ChainRestoreGeometry.Finite(new V(0, float.PositiveInfinity, 0)) &&
               ChainRestoreGeometry.Finite(world), "invalid particles are rejected");
        var oldLocal = new V(0, -1.4f, 0);
        var anchorLocal = new V(.055f, -.039f, 0);
        Check((ChainRestoreGeometry.ResolveLegacyRoot(oldLocal, position + oldLocal, anchorLocal,
            position + anchorLocal, .08f, true) - anchorLocal).magnitude < .00001f,
            "legacy attached snapshot with a 1.4m stale root reconnects to saved collar ring settings");
        var validLocal = anchorLocal + new V(.01f, 0, 0);
        Check((ChainRestoreGeometry.ResolveLegacyRoot(validLocal, position + validLocal, anchorLocal,
            position + anchorLocal, .08f, true) - validLocal).magnitude < .00001f,
            "valid legacy contact retains its saved offset");
        Check((ChainRestoreGeometry.ResolveLegacyRoot(oldLocal, position + oldLocal, anchorLocal,
            position + anchorLocal, .08f, false) - oldLocal).magnitude < .00001f,
            "detached roots are never silently attached to the collar");

        const int count = 24;
        var points = new V[count];
        var previous = new V[count];
        var lengths = new float[count - 1];
        for (int i = 0; i < count; ++i)
        {
            points[i] = previous[i] = new V(i * .05f, -.15f * (float)Math.Sin(i * Math.PI / (count - 1)), 0);
            if (i > 0) lengths[i - 1] = (points[i] - points[i - 1]).magnitude;
        }
        var original = (V[])points.Clone();
        var tip = points[count - 1];
        var newRoot = points[0] + new V(.1f, .1f, 0);
        ChainSimulation.Step(points, previous, lengths, newRoot, tip, true, true, 0, 0, null, true);
        Check((points[0] - newRoot).magnitude < .00001f && (points[count - 1] - tip).magnitude < .00001f,
            "finite restore projection attaches collar root while preserving saved room-fixed tip");
        Array.Copy(points, previous, count);
        for (int i = 0; i < count; ++i)
        {
            Check(ChainRestoreGeometry.Finite(points[i]), "all restored particles stay finite");
            Check((points[i] - previous[i]).magnitude < .00001f, "restoration clears inherited velocity");
        }
        ChainSimulation.Step(points, previous, lengths, newRoot, tip, true, true, 0, 0, null, true);
        Check((points[0] - newRoot).magnitude < .00001f && (points[count - 1] - tip).magnitude < .00001f,
            "next constraint solve does not undo restored endpoints");
        Console.WriteLine($"Chain restore geometry: PASS ({checks} assertions)");
    }
}
