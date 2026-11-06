// Dependencies only: the reference inventory algorithm itself is compiled directly
// from the extracted AIProject.StuffItemExtensions.cs, not rewritten in this test.
internal static class Singleton<T> where T : new() { public static T Instance { get; } = new T(); }
namespace Manager
{
    public sealed class Resources { public DefinePack DefinePack { get; } = new DefinePack(); }
    public sealed class DefinePack { public MapDefines MapDefines { get; } = new MapDefines(); }
    public sealed class MapDefines { public int ItemSlotMax { get; set; } }
}
namespace AIProject.SaveData
{
    public sealed class StuffItem
    {
        public int CategoryID, ID, Count;
        public System.DateTime LatestDateTime;
        public StuffItem() { }
        public StuffItem(StuffItem source)
        { CategoryID = source.CategoryID; ID = source.ID; Count = source.Count; LatestDateTime = source.LatestDateTime; }
    }
}
