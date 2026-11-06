using System;
using System.Globalization;
using System.IO;
using System.Linq;
using MapEnvironmentRuntime;
using UnityEngine;

namespace MainGameAiShoujoMode
{
    // Own a navigation world only for the independent island session.
    internal sealed class AiModeNavigation : IDisposable
    {
        private readonly SandboxHost _host;
        private readonly AiAnimationTrial _girl;
        private readonly string _content;
        private readonly Action<string> _log;
        private readonly Action<Exception> _error;
        private readonly NavigationWorld _world = new NavigationWorld();
        internal NavigationWorld World=>_world;
        private string _signature;
        private bool _disposed;
        internal Func<System.Collections.Generic.IReadOnlyList<Bounds>> FurnitureBounds {get;set;}
        internal void RefreshFurniture(){_world.RefreshBlockedVolumes();}
        internal AiModeNavigation(SandboxHost host, AiAnimationTrial girl, string content, Action<string> log, Action<Exception> error)
        {
            _host = host; _girl = girl; _content = content; _log = log; _error = error;
            _host.Session.Navigation = _world;
            _world.SetBlockedVolumes(() => _host.Island.Doors.ClosedBounds().Concat(FurnitureBounds?.Invoke()??Array.Empty<Bounds>()).ToArray());
            _world.SetCollisionFilter(collider => collider.gameObject.scene == _host.Island.LoadedScene && !Physics.GetIgnoreLayerCollision(_host.Session.Settings.I("math.zero"), collider.gameObject.layer));
            try { Build(_host.Session.Settings); }
            catch { _world.Dispose(); _host.Session.Navigation = null; throw; }
            _host.SetWalkingNavigation(_world);
            _girl.SetNavigation(_world);
            _host.Session.ParametersApplied += Changed;
            _host.Island.Doors.PassageChanged += PassageChanged;
        }
        private static bool Relevant(string key) => key.StartsWith("navigation.", StringComparison.Ordinal) || key.StartsWith("island.stage.", StringComparison.Ordinal) || key == "island.scale";
        private static string Signature(Parameters p) => string.Join("|", p.numbers.Where(item => Relevant(item.key)).Select(item => item.key + "=" + item.value.ToString(CultureInfo.InvariantCulture)))
            + string.Join("|", p.text.Where(item => Relevant(item.key)).Select(item => item.key + "=" + item.value));
        private void Build(Parameters p)
        {
            _world.Build(p, Path.Combine(_content, p.T("navigation.source")), Path.Combine(_content, p.T("navigation.waypoints")), _host.FromIslandSourcePosition);
            _signature = Signature(p);
            _log("AI_MODE_NAVIGATION_READY sources=" + _world.Status.sourceMeshes + " waypoints=" + _world.Status.acceptedWaypoints + " rejected=" + _world.Status.rejectedWaypoints);
        }
        private void Changed(Parameters p)
        {
            if (Signature(p) == _signature) return;
            try { Build(p); }
            catch (Exception ex) { _error(ex); }
        }
        private void PassageChanged() { _world.RefreshBlockedVolumes(); _girl.SetNavigation(_world); }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            if (_host != null)
            {
                _host.SetWalkingNavigation(null);
                if (_host.Session != null) _host.Session.ParametersApplied -= Changed;
                if (_host.Island?.Doors != null) _host.Island.Doors.PassageChanged -= PassageChanged;
            }
            if (_girl != null) _girl.SetNavigation(null); _world.Dispose();
            if (_host != null && _host.Session != null && _host.Session.Navigation == _world) _host.Session.Navigation = null;
        }
    }
}
