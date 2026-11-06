using System;
using System.Collections.Generic;
using System.Linq;

namespace MainGameVoiceFaceEventBridge
{
    // Pure name matching; runtime identity and Unity access stay in Plugin.Clothing.cs.
    internal sealed class ClothingSnapshot
    {
        internal static readonly string[] Parts = { "トップス", "ボトムス", "ブラ", "ショーツ", "手袋", "パンスト", "靴下", "上履き", "外履き" };
        internal string Token;
        internal string CoordinateName;
        internal int CoordinateIndex;
        internal int Epoch, Main;
        internal object Actor, Coordinate;
        internal readonly string[] Names = new string[9];
        internal readonly int[] Ids = new int[9];
        internal readonly Dictionary<string, ClothingPiece> Clothing = new Dictionary<string, ClothingPiece>();
        internal AccessoryPiece Glasses;

        internal void AddAccessory(string name, bool visible)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Accessory name missing");
            // One user-facing eye item: visible first, then stable slot order.
            if (Glasses == null || (visible && Glasses.State != "装着"))
                Glasses = new AccessoryPiece { Name = name, State = visible ? "装着" : "外している" };
        }

        internal void Add(int kind, int id, bool equipped, string name, string state)
        {
            Ids[kind] = id;
            if (!equipped) { Clothing.Add(Parts[kind], null); return; }
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(state))
                throw new InvalidOperationException("Clothing name or state missing");
            Names[kind] = name;
            Clothing.Add(Parts[kind], new ClothingPiece { Name = name, State = state });
        }

        internal bool MatchesIdentity(int epoch, int main, object actor, object coordinate, int[] ids, string[] names)
            => Epoch == epoch && Main == main && ReferenceEquals(Actor, actor) && ReferenceEquals(Coordinate, coordinate)
               && Ids.SequenceEqual(ids) && Names.SequenceEqual(names);

        internal sealed class Hit
        {
            internal int Kind, Index;
            internal string Name;
        }

        internal List<Hit> Match(string line, out string masked)
        {
            var hits = new List<Hit>();
            char[] mask = line.ToCharArray();
            bool[] occupied = new bool[line.Length];
            // Include the coordinate only to mask coordinate-only mentions from the
            // legacy generic-word parser. Real garment names win exact-name ties.
            foreach (string name in Names.Concat(new[] { CoordinateName }).Where(n => !string.IsNullOrEmpty(n)).Distinct().OrderByDescending(n => n.Length))
            {
                for (int start = 0; start <= line.Length - name.Length;)
                {
                    int pos = line.IndexOf(name, start, StringComparison.Ordinal);
                    if (pos < 0) break;
                    start = pos + name.Length;
                    if (Enumerable.Range(pos, name.Length).Any(i => occupied[i])) continue;
                    for (int i = pos; i < start; i++) { occupied[i] = true; mask[i] = ' '; }
                    int action = start;
                    while (action < line.Length && char.IsWhiteSpace(line[action])) action++;
                    if (action < line.Length && line[action] == 'を') action++;
                    while (action < line.Length && char.IsWhiteSpace(line[action])) action++;
                    if (line.IndexOf("脱ぐね", action, StringComparison.Ordinal) != action) continue;
                    // Even an ambiguous name owns its action. Never let an earlier generic
                    // part word reinterpret it and undress a different garment.
                    for (int i = action; i < action + "脱ぐね".Length; i++) mask[i] = ' ';
                    int[] kinds = Enumerable.Range(0, Names.Length).Where(i => Names[i] == name).ToArray();
                    if (kinds.Length > 1)
                    {
                        // Only the adjacent explicit part label disambiguates identical names.
                        string prefix = line.Substring(0, pos).TrimEnd(' ', '　', 'の', ':', '：', '「', '『');
                        kinds = kinds.Where(i => prefix.EndsWith(Parts[i], StringComparison.Ordinal)).ToArray();
                    }
                    if (kinds.Length == 1) hits.Add(new Hit { Kind = kinds[0], Index = action, Name = name });
                }
            }
            masked = new string(mask);
            return hits;
        }
    }
}
