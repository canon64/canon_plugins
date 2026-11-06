using System.Collections;
using System.IO;
using System.Linq;
namespace MainGameAiShoujoMode
{
    internal sealed partial class HotelModeSession
    {
        private IEnumerator EnsureFloorLoaded(HotelFloorRuntime destination)
        {
            if (destination.Roots != null) yield break;
            var shared = _floors.FirstOrDefault(f => f != destination && f.LoadedBundle != null
                && f.Definition.bundle == destination.Definition.bundle)?.LoadedBundle;
            yield return destination.Load(Path.Combine(_receptionContent, "Hotel"), shared,
                _diagnostics() ? (System.Action<string>)(stage => { if (_diagnostics()) TraceRoom("map-" + stage); }) : null);
        }
    }
}
