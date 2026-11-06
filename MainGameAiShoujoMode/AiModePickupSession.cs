using System;
using MainGameAiShoujoMode.Life;
using MapEnvironmentRuntime;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    // Placeholder geometry, real acquisition. All work happens on place/F/exit events.
    internal sealed class AiModePickupSession : IDisposable
    {
        private readonly SandboxHost _host;
        private readonly AiLifeRuntime _life;
        private GameObject _marker;
        private bool _disposed;
        internal string LastMessage { get; private set; }
        internal Transform MomentMarker => _marker == null ? null : _marker.transform;
        internal void RestoreMoment(Vector3 position, Quaternion rotation)
        {
            if (_marker != null) UnityEngine.Object.Destroy(_marker);
            _marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _marker.name = "仮拾得物（風邪薬） Fで拾う"; _marker.transform.SetParent(_host.transform, true);
            _marker.transform.SetPositionAndRotation(position, rotation);
            _marker.transform.localScale = Vector3.one * _life.ConnectionCatalog.pickup.size;
        }
        internal AiModePickupSession(SandboxHost host, AiLifeRuntime life) { _host = host; _life = life; }
        private AiLifeConnection Request()
        {
            var settings = _life.ConnectionCatalog.pickup;
            return new AiLifeConnection { key = settings.key, kind = "pickup", category = settings.category, item = settings.item, count = settings.count };
        }
        internal string Place()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AiModePickupSession));
            var camera = _host.Walker?.ViewCamera;
            if (camera == null) throw new InvalidOperationException("配置に使うカメラがありません。");
            var settings = _life.ConnectionCatalog.pickup;
            var request = Request();
            var committed = _life.Store.Read().operations.Exists(receipt => receipt.request.id == "pickup:" + _life.PlayerInventoryKey + ":" + request.key);
            if (committed) return LastMessage = "この仮拾得物は取得済みです。再生成しません。";
            var forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
            var point = camera.transform.position + forward * settings.forward + camera.transform.right * settings.right;
            if (!Physics.Raycast(point + Vector3.up * settings.groundProbe, Vector3.down, out var ground,
                settings.groundProbe + settings.groundProbe, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return LastMessage = "配置先の地面が見つかりません。場所を変えてください。";
            if (_marker != null) UnityEngine.Object.Destroy(_marker);
            _marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _marker.name = "仮拾得物（風邪薬） Fで拾う"; _marker.transform.SetParent(_host.transform, true);
            _marker.transform.position = ground.point + Vector3.up * settings.height; _marker.transform.localScale = Vector3.one * settings.size;
            return LastMessage = "目の前に仮拾得物の箱を置きました。会話を閉じ、箱に照準を合わせFで拾えます。";
        }
        internal bool TryInteract()
        {
            if (_disposed || _marker == null || _host.Walker?.ViewCamera == null) return false;
            var camera = _host.Walker.ViewCamera;
            if (!Physics.Raycast(camera.transform.position, camera.transform.forward, out var hit, _life.ConnectionCatalog.pickup.reach,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || hit.collider.gameObject != _marker) return false;
            try
            {
                LastMessage = _life.Pickup(Request()).Message;
                UnityEngine.Object.Destroy(_marker); _marker = null;
            }
            catch (Exception error) { LastMessage = error.GetBaseException().Message; }
            return true;
        }
        public void Dispose() { _disposed = true; if (_marker != null) UnityEngine.Object.Destroy(_marker); _marker = null; }
    }
}
