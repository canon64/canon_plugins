namespace UnityEngine
{
    public struct Vector3 { public float x,y,z; public override string ToString()=> $"({x},{y},{z})"; public string ToString(string f)=>ToString(); }
    public struct Quaternion { public float w; public override string ToString()=> $"(0,0,0,{w})"; public string ToString(string f)=>ToString(); }
    public class Transform
    {
        public static int Reads;
        public string name="test";
        public Transform parent;
        public Vector3 position,localPosition;
        public Quaternion rotation = new Quaternion { w=1 };
        public int GetInstanceID(){Reads++;return GetHashCode();}
        public bool IsChildOf(Transform root){for(var p=this;p!=null;p=p.parent)if(p==root)return true;return false;}
    }
    public static class Time { public static int frameCount; }
}
public class ChaControl { public UnityEngine.Transform transform=new UnityEngine.Transform(); }
namespace RootMotion.FinalIK
{
    public class IKSolverFullBodyBiped
    {
        public event Action OnPostUpdate;
        public int Subscribers => OnPostUpdate?.GetInvocationList().Length??0;
        public void Solve()=>OnPostUpdate?.Invoke();
    }
    public class IKEffector { public UnityEngine.Transform target,bone; public float positionWeight=1; }
    public class IKConstraintBend { public UnityEngine.Transform bendGoal,bone2; public float weight; }
}
namespace BepInEx.Bootstrap
{
    public class PluginInfo { public object Instance; }
    public static class Chainloader { public static Dictionary<string,PluginInfo> PluginInfos=new(); }
}
namespace BepInEx.Logging
{
    public class ManualLogSource
    {
        public int Writes;
        public void LogInfo(string text)=>Writes++;
        public void LogWarning(string text)=>Writes++;
        public void LogError(string text)=>Writes++;
    }
}
namespace MainGameLogRelay
{
    public static class LogRelayApi
    {
        public static bool IsAvailable;
        public static int Writes;
        public static void SetOwnerLogKey(string a,string b){}
        public static void SetOwnerEnabled(string a,bool b){}
        public static void Info(string a,string b)=>Writes++;
        public static void Warn(string a,string b)=>Writes++;
        public static void Error(string a,string b)=>Writes++;
        public static void Debug(string a,string b)=>Writes++;
    }
}
namespace CanonPlugins.Local { public static class SessionGate { public static bool Allowed=true; } }
namespace MainGameBlankMapAdd
{
    public sealed partial class Plugin
    {
        private ChaControl _femaleChara;
        public Plugin(ChaControl actor)=>_femaleChara=actor;
        public void Move(string action="recall",string place="room201")
        {var id=BeginHLocationNotification(action,place);_femaleChara.transform.position=new UnityEngine.Vector3{x=100};EndHLocationNotification(id,action,place);}
        public int Subscribers=>HLocationMoving?.GetInvocationList().Length??0;
    }
}
namespace MainGirlHipHijack
{
    public sealed partial class Plugin
    {
        private class Settings { public bool BodyIkDiagnosticLog; public bool[] FollowComposerEnabled=new bool[1]; }
        private class ActorSession { public ChaControl Character; public BodyIk FullBodyIk=new(); }
        public class BodyIk { public RootMotion.FinalIK.IKSolverFullBodyBiped solver=new(); }
        private class Point { public bool IsBendGoal,Running=true; public UnityEngine.Transform Proxy,FollowBone; }
        private Settings _settings=new();
        private ActorSession _femaleSession=new();
        private Point[] _bikEff=new Point[1];
        private bool[] _bikWant=new[]{true};
        private const int BIK_TOTAL=1;
        private UnityEngine.Transform _ikTargetRoot;
        private Services.HipHijackLogger _hipHijackLogger;
        private bool _enabled=true;
        private RootMotion.FinalIK.IKEffector _effector;
        public Plugin(ChaControl actor,string directory)
        {
            _femaleSession.Character=actor;
            _ikTargetRoot=new UnityEngine.Transform{parent=actor.transform,name="folder"};
            _bikEff[0]=new Point{Proxy=new UnityEngine.Transform{parent=_ikTargetRoot,name="proxy"}};
            _effector=new RootMotion.FinalIK.IKEffector{target=_bikEff[0].Proxy,bone=new UnityEngine.Transform{name="hip"}};
            _hipHijackLogger=new Services.HipHijackLogger(directory,"hip","hip","hip","input","input",new BepInEx.Logging.ManualLogSource(),()=>false,()=>false);
        }
        private bool IsPluginEnabled()=>_enabled&&CanonPlugins.Local.SessionGate.Allowed;
        private RootMotion.FinalIK.IKEffector BIK_GetEffector(int i)=>_effector;
        private RootMotion.FinalIK.IKConstraintBend BIK_GetBendConstraint(int i)=>null;
        private float GetBodyIKWeight(int i)=>1;
        private void LogBodyIkDiag(string text){if(_settings.BodyIkDiagnosticLog)_hipHijackLogger.DiagnosticInfo(text);}
        private void LogBodyIkDiagWarn(string text){if(_settings.BodyIkDiagnosticLog)_hipHijackLogger.DiagnosticWarning(text);}
        public void SetDiagnostic(bool enabled){_settings.BodyIkDiagnosticLog=enabled;ApplyHLocationDiagnosticState();}
        public void Late()=>TraceHLocationAfterNativeLateUpdate();
        public void Stop()=>DisconnectHLocationDiagnostics();
        public void Start()=>ApplyHLocationDiagnosticState();
        public void Solve()=>_femaleSession.FullBodyIk.solver.Solve();
        public int SolverSubscribers=>_femaleSession.FullBodyIk.solver.Subscribers;
        public void Write(string source)=>TraceHLocationIkWrite(0,source,new UnityEngine.Vector3{x=2,z=-64},new UnityEngine.Quaternion{w=1},"world");
        public void LoadPreset()=>TraceHLocationPresetLoad(0,"preset-load",new UnityEngine.Vector3{x=2,z=-64},new UnityEngine.Quaternion{w=1},"world");
        public void UseOtherBinding()=>_effector.target=new UnityEngine.Transform{name="other-target"};
    }
}
