using System;
using System.Reflection;

namespace MainGameAiShoujoMode
{
    // Operation-time merge. Serialized status contains only the twenty native accessory slots.
    internal static class CharacterStatusRestore
    {
        private static readonly PropertyInfo[] Properties = typeof(ChaFileStatus).GetProperties(BindingFlags.Instance | BindingFlags.Public);

        internal static ChaFileStatus Merge(ChaFileStatus current, ChaFileStatus saved, int tearsLevelCount)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            var result = new ChaFileStatus();
            foreach (var property in Properties)
            {
                if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length != default(int)) continue;
                object live = property.GetValue(current, null);
                object value = saved == null ? null : property.GetValue(saved, null);
                string name = property.Name;
                // These identify the loaded clothing/objects, not their saved display state.
                bool preserve = name == "version" || name == "coordinateType" || name == "backCoordinateType" || name == "shoesType";
                if (live is Array array)
                {
                    var merged = (Array)array.Clone();
                    if (!preserve && value is Array source && source.Rank == array.Rank)
                    {
                        if (array.Rank == typeof(bool[]).GetArrayRank())
                        {
                            for (int i = default(int); i < Math.Min(array.Length, source.Length); ++i)
                                if (ValidElement(name, source.GetValue(i))) merged.SetValue(source.GetValue(i), i);
                        }
                        else if (array.Rank == typeof(bool[,]).GetArrayRank())
                        {
                            for (int i = default(int); i < Math.Min(array.GetLength(default(int)), source.GetLength(default(int))); ++i)
                                for (int j = default(int); j < Math.Min(array.GetLength(typeof(bool[]).GetArrayRank()), source.GetLength(typeof(bool[]).GetArrayRank())); ++j)
                                    if (ValidElement(name, source.GetValue(i, j))) merged.SetValue(source.GetValue(i, j), i, j);
                        }
                    }
                    property.SetValue(result, merged, null);
                }
                else
                {
                    bool valid = value != null && !(value is float f && (float.IsNaN(f) || float.IsInfinity(f)));
                    if (name == "tearsLv") valid &= value is byte tears && tears < tearsLevelCount;
                    property.SetValue(result, !preserve && valid ? value : live, null);
                }
            }
            return result;
        }

        private static bool ValidElement(string name, object value)
        {
            if (name == "clothesState") return value is byte state && state < ChaFileDefine.alphaState.GetLength(default(int));
            return !(value is float f && (float.IsNaN(f) || float.IsInfinity(f)));
        }
    }
}
