using System.IO;
using BepInEx;
using MapEnvironmentRuntime;
using UnityEngine;

namespace MainGameMapSandbox
{
    [BepInDependency("canon.gate", "2.3.0")]
    [BepInPlugin("com.kks.maingame.mapsandbox", "MainGameMapSandbox", "1.0.0")]
    [BepInProcess("KoikatsuSunshine")]
    [BepInProcess("KoikatsuSunshine_VR")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private SandboxSession _session;
        private Parameters _parameters;
        private bool _acceptAttachments;
        private const string Guid = "com.kks.maingame.mapsandbox";
        private void Awake() { _parameters = Parameters.Load(GetParametersPath()); CanonPlugins.Shared.PluginActivityGate.Bind(this, Guid, CanonGate.GateApi.ActivityKind.Preview, () => { _acceptAttachments = true; }, StopSession); }
        private void OnDestroy() => CanonPlugins.Shared.PluginActivityGate.Dispose();
        private string Content => Path.Combine(Path.GetDirectoryName(Info.Location), "Content");
        public string GetBundlePath() => Path.Combine(Content, _parameters.T("content.bundle"));
        public string GetParametersPath() => Path.Combine(Content, "parameters.json");
        public string GetSceneName() => _parameters.T("content.scene");
        public string GetAnchorName() => _parameters.T("content.anchor");
        public byte GetActorSex() => checked((byte)_parameters.N("kks.actorSex"));
        public bool IsEnvironmentActive() => _session != null;
        public void DrawEnvironmentSettings() { if(CanonPlugins.Shared.PluginActivityGate.Allowed && _session!=null) _session.DrawEnvironmentSettings(); }
        private void StopSession()
        {
            _acceptAttachments = false;
            if(_session==null) return;
            if(_session.World!=null) Destroy(_session.World);
            Destroy(_session);_session=null;
        }
        public void AttachMap(GameObject map)
        {
            CanonPlugins.Shared.PluginActivityGate.Begin();
            if (!CanonPlugins.Shared.PluginActivityGate.Allowed || !_acceptAttachments) throw new System.InvalidOperationException("Map environment is not authorized for this activity");
            if (_session != null) throw new System.InvalidOperationException("An environment is already attached");
            _session = map.AddComponent<SandboxSession>();
            try { _session.Initialize(map.transform, GetParametersPath(), false); }
            catch { DestroyImmediate(_session); _session = null; throw; }
            Logger.LogInfo("Shared map environment attached");
        }
        private void OnDisable() => CanonPlugins.Shared.PluginActivityGate.End();
    }
}
