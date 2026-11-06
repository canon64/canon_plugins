namespace UnityEngine { public class MonoBehaviour { public bool enabled=true; } }
namespace BepInEx { public class BaseUnityPlugin : UnityEngine.MonoBehaviour {} }
public class ChaControl {}
public class HSceneProc : UnityEngine.MonoBehaviour {
 public System.Collections.Generic.List<ChaControl> lstFemale=new System.Collections.Generic.List<ChaControl>();
 public ChaControl male,male1;
}
namespace KKAPI.Studio {
 public static class StudioAPI { public static bool StudioLoaded; public static event System.EventHandler StudioLoadedChanged;
 public static void Set(bool value){StudioLoaded=value;StudioLoadedChanged?.Invoke(null,System.EventArgs.Empty);} }
}
