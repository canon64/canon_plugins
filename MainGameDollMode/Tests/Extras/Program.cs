using System;
namespace UnityEngine { public class Object {} }
public class ChaControl : UnityEngine.Object
{
    public byte Liquid = 2;
    public int GetInstanceID() => 1;
    public bool Target = true;
    public object Controller = new KK_SkinEffects.SkinEffectsController();
    public object GetComponent(Type type) => Controller;
}
namespace KK_SkinEffects
{
    public class SkinEffectsController : UnityEngine.Object
    {
        private int tears = 1;
        public bool FailOnce;
        public int TearLevel { get => tears; set { tears = value; if (FailOnce) { FailOnce = false; throw new Exception("partial setter failure"); } } }
        public int DroolLevel { get; set; }
        public int SalivaLevel { get; set; }
        public int SweatLevel { get; set; }
        public int CumInNoseLevel { get; set; }
    }
}
namespace CanonPlugins.Shared { static class IndependentActorFilter { public static bool IsIndependent(ChaControl actor) => false; } }
namespace CanonPlugins.Local { static class SessionGate { public static bool Allowed; public static ChaControl[] Actors = new ChaControl[0]; } }
namespace MainGameDollMode
{
    public partial class Plugin
    {
        public const string GUID = "canon.maingame.dollmode";
        private PluginSettings _settings = new PluginSettings { Enabled = true, DollModeEnabled = true, DollModeModEffectsEnabled = true, DollModeModTears = 3, DollModeModDrool = 1 };
        private bool IsTargetCharacter(ChaControl actor) => actor.Target;
        private sealed class FaceState { public byte FaceSiruLevel; }
        private readonly System.Collections.Generic.Dictionary<int, FaceState> _originalFaceExpressionByCha = new System.Collections.Generic.Dictionary<int, FaceState>();
        private sealed class WriteScope : IDisposable { public void Dispose() {} }
        private IDisposable BeginInternalFaceWrite() => new WriteScope();
        private static byte ReadCurrentFaceSiruLevel(ChaControl actor) => actor.Liquid;
        private bool TrySetFaceSiruLevel(ChaControl actor, byte level, out bool changed) { changed = actor.Liquid != level; actor.Liquid = level; return true; }
        private void LogWarn(string message) => Console.WriteLine(message);
        private static int checks;
        private static void Check(bool result, string message) { checks++; if (!result) throw new Exception(message); }
        public static void Main()
        {
            var p = new Plugin(); var actor = new ChaControl();
            var control = (KK_SkinEffects.SkinEffectsController)actor.Controller;
            CanonPlugins.Local.SessionGate.Actors = new[] { actor };
            p.SyncPresetExtras(); Check(control.TearLevel == 1, "denied wrote effects");
            CanonPlugins.Local.SessionGate.Allowed = true;
            p.SyncPresetExtras(); Check(control.TearLevel == 3 && control.DroolLevel == 1, "apply failed");
            Check(actor.Liquid == 0, "explicit native liquid selection ignored while external writes allowed");
            p._settings.DollModeModTears = 2; p.SyncPresetExtras();
            CanonPlugins.Local.SessionGate.Allowed = false; p.ReleasePresetExtras(); p.ReleasePresetExtras();
            Check(control.TearLevel == 1 && control.DroolLevel == 0 && p._presetExtras.Count == 0, "revoked/double cleanup lost original");
            Check(actor.Liquid == 2, "native liquid not restored after revoke");
            CanonPlugins.Local.SessionGate.Allowed = true; p.SyncPresetExtras();
            control.TearLevel = 0; p.ReleasePresetExtras(); Check(control.TearLevel == 0, "external change overwritten");
            control.TearLevel = 1; p.SyncPresetExtras();
            CanonPlugins.Local.SessionGate.Actors = new ChaControl[0]; p.SyncPresetExtras();
            Check(control.TearLevel == 1 && p._presetExtras.Count == 0, "removed actor not restored");
            CanonPlugins.Local.SessionGate.Actors = new[] { actor }; control.FailOnce = true;
            p.SyncPresetExtras(); Check(control.TearLevel == 1, "partial failure not restored");
            p._settings.Enabled = false; p.SyncPresetExtras(); Check(p._presetExtras.Count == 0 && control.TearLevel == 1, "disabled wrote effects");
            p._settings.Enabled = true; p._settings.DollModeSpiralOverlayEnabled = true; actor.Controller = null;
            p.SyncPresetExtras(); Check(p._presetExtraStatus.Contains("未適用"), "missing optional component not reported");
            p.ReleasePresetExtras();
            Console.WriteLine("PASS: actual preset extras: " + checks + " lifecycle/ownership checks; visual rendering not tested.");
        }
    }
}
