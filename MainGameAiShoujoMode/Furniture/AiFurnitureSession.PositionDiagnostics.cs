using System;
using System.Linq;
using MainGameAiShoujoMode.Conversation;

namespace MainGameAiShoujoMode.Furniture
{
    internal sealed partial class AiFurnitureSession
    {
        // Only invoked inside RecordEventPositions' lazy enabled guard.
        internal EventPositionSnapshot.FurniturePoint[] CaptureEventPositions()
            => _placed.Values.Where(placed => placed.Root != null)
                .SelectMany(placed => (placed.Item.slots ?? Array.Empty<FurnitureSlot>()).Select(slot =>
                    new EventPositionSnapshot.FurniturePoint
                    {
                        instanceId = placed.Data.id, name = placed.Item.name, slotId = slot.id, slotStatus = slot.status,
                        root = EventPositionSnapshot.Point.Capture(_host, placed.Root.transform),
                        slot = EventPositionSnapshot.Point.Capture(_host, placed.Root.transform.TransformPoint(Vector(slot.position)))
                    })).ToArray();
    }
}
