using System;
using System.Reflection;
using MainGamePregnancyPlusBridge;

public class ChaControl : UnityEngine.MonoBehaviour { }
namespace MainGamePregnancyPlusBridge
{
    public sealed partial class Plugin
    {
        private Type _controllerType = typeof(FakeController);
        private MethodInfo _meshInflateByDataMethod = typeof(FakeController).GetMethod(nameof(FakeController.MeshInflate));
        private bool _hasRuntimeInflationSizeOverride, _dirty;
        private object _bellyHSceneProc, _bellyFallbackSceneProc, _applyCharactersCache;
        private void ResetBellyPeakTracking() { }
        private void LogWarnOnce(string key, string message) { }
        private void LogWarn(string message) { throw new Exception(message); }
        private static Exception Unwrap(Exception ex) => ex;
        public void TestRemember(ChaControl cha, object controller) { if (!CaptureInflation(cha, controller)) throw new Exception("snapshot rejected"); }
        public void TestEnd() => EndGateSession();
    }
    public sealed class FakeData
    {
        public float Size;
        public FakeData Clone() => new FakeData { Size = Size };
    }
    public sealed class FakeController
    {
        public FakeData infConfig = new FakeData { Size = 7 };
        public int Restores;
        public void MeshInflate(FakeData data, string owner, object options) { infConfig = data; Restores++; }
    }
}
internal static class PregnancyRestoreTests
{
    internal static void Run(Action<bool,string> check)
    {
        var plugin = new Plugin(); var cha = new ChaControl(); var controller = new FakeController();
        plugin.TestRemember(cha, controller);
        controller.infConfig.Size = 35;
        plugin.TestRemember(cha, controller);
        plugin.TestEnd();
        check(controller.infConfig.Size == 7, "Pregnancy production rollback restores deep snapshot rather than later modified data");
        check(controller.Restores == 1, "repeated application retains the first original inflation state");
        plugin.TestEnd();
        check(controller.Restores == 1, "duplicate gate stop does not inflate or restore twice");
    }
}
