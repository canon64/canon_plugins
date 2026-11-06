using System;

namespace MainGameHLocationPlacement
{
    internal static class Compatibility
    {
        internal static void CheckMap(Type type)
            => MapContract.Check(type, typeof(ChaControl), typeof(BaseMap));
    }
}
