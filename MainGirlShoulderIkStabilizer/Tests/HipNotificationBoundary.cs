using System.Reflection;
using RootMotion.FinalIK;

namespace MainGirlHipHijack
{
    // Only engine/session access is replaced. The notification/solver hooks are production code.
    public sealed partial class Plugin
    {
        public static Plugin Instance;
        private readonly FemaleSession _femaleSession = new();
        private sealed class FemaleSession { public FullBodyBipedIK FullBodyIk; public ChaControl Character; }
        public void Setup(FullBodyBipedIK ik)
        {
            Instance = this;
            _femaleSession.FullBodyIk = ik; _femaleSession.Character = new ChaControl();
            PublishShoulderRuntime();
        }
        public void Clear() => ClearShoulderRuntime();
        public void Solve(Action body)
        {
            var before = typeof(ShoulderSolverPatches).GetMethod("Before", BindingFlags.Static | BindingFlags.NonPublic);
            var after = typeof(ShoulderSolverPatches).GetMethod("After", BindingFlags.Static | BindingFlags.NonPublic);
            object[] args = { _femaleSession.FullBodyIk.solver, default(ShoulderSolveScope) };
            before.Invoke(null, args);
            Exception failure = null;
            try { body(); } catch (Exception ex) { failure = ex; }
            var result = (Exception)after.Invoke(null, new[] { failure, args[1] });
            if (result != null) throw result;
        }
    }
}
