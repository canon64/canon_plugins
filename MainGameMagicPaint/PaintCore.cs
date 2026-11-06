using System;
using System.Collections.Generic;
using System.Linq;

namespace MainGameMagicPaint
{
    // Game-independent editing core. Each operation stores only changed 32x32 tiles.
    internal sealed class PaintBuffer
    {
        internal const int Tile = 32;
        internal readonly int Size;
        internal readonly uint[] Pixels;
        readonly Dictionary<int, uint[]> _before = new Dictionary<int, uint[]>();
        internal bool Dirty;
        internal int InkPixels { get; private set; }
        internal long Revision { get; private set; }
        internal readonly HashSet<int> UploadTiles = new HashSet<int>();
        internal PaintBuffer(int size) { if (size < Tile || size % Tile != 0) throw new ArgumentOutOfRangeException(nameof(size)); Size = size; Pixels = new uint[size * size]; }
        internal static uint Pack(byte r, byte g, byte b, byte a) => (uint)(r | g << 8 | b << 16 | a << 24);
        static byte Byte(float value) => (byte)Math.Max(0, Math.Min(255, (int)Math.Round(value)));
        internal static uint Blend(uint dst, uint rgb, float strength, bool erase)
        {
            float sa = Math.Max(0, Math.Min(1, strength)), da = (dst >> 24) / 255f;
            if (erase) { byte a = Byte((1 - sa) * da * 255); return a <= 1 ? 0 : (dst & 0xffffff) | ((uint)a << 24); }
            float oa = sa + da * (1 - sa); if (oa <= 0) return 0;
            return Pack(Byte(((rgb & 255) * sa + (dst & 255) * da * (1 - sa)) / oa),
                Byte((((rgb >> 8) & 255) * sa + ((dst >> 8) & 255) * da * (1 - sa)) / oa),
                Byte((((rgb >> 16) & 255) * sa + ((dst >> 16) & 255) * da * (1 - sa)) / oa), Byte(oa * 255));
        }
        internal void Set(int x, int y, uint value)
        {
            if ((uint)x >= Size || (uint)y >= Size) return;
            int at = y * Size + x; if (Pixels[at] == value) return;
            int tile = (y / Tile) * (Size / Tile) + x / Tile;
            if (!_before.ContainsKey(tile)) _before.Add(tile, CopyTile(tile));
            if ((Pixels[at] >> 24) != 0) InkPixels--; if ((value >> 24) != 0) InkPixels++;
            Pixels[at] = value; Revision++; Dirty = true; UploadTiles.Add(tile);
        }
        uint[] CopyTile(int tile)
        {
            var result = new uint[Tile * Tile]; int x = tile % (Size / Tile) * Tile, y = tile / (Size / Tile) * Tile;
            for (int row = 0; row < Tile; row++) Array.Copy(Pixels, (y + row) * Size + x, result, row * Tile, Tile);
            return result;
        }
        internal void Restore(int tile, uint[] pixels)
        {
            int x = tile % (Size / Tile) * Tile, y = tile / (Size / Tile) * Tile;
            for (int row = 0; row < Tile; row++) for (int col = 0; col < Tile; col++)
            {
                int at = (y+row)*Size+x+col; if ((Pixels[at]>>24)!=0) InkPixels--;
                uint pixel = pixels[row*Tile+col]; if(Pixels[at] != pixel) Revision++; Pixels[at] = pixel; if ((pixel>>24)!=0) InkPixels++;
            }
            Dirty = true; UploadTiles.Add(tile);
        }
        internal List<TileEdit> Commit()
        {
            var edits = new List<TileEdit>();
            foreach (var item in _before) { var after = CopyTile(item.Key); if (!item.Value.SequenceEqual(after)) edits.Add(new TileEdit { Buffer = this, Tile = item.Key, Before = item.Value, After = after }); }
            _before.Clear(); return edits;
        }
        internal void Clear() { for (int i = 0; i < Pixels.Length; i++) if (Pixels[i] != 0) Set(i % Size, i / Size, 0); }
        internal void Replace(uint[] source)
        { if (source.Length != Pixels.Length) throw new ArgumentException("画像サイズが異なります"); for (int i = 0; i < source.Length; i++) Set(i % Size, i / Size, source[i]); }
    }
    internal sealed class TileEdit { internal PaintBuffer Buffer; internal int Tile; internal uint[] Before, After; }
    internal sealed class PaintHistory
    {
        readonly List<List<TileEdit>> _undo = new List<List<TileEdit>>(), _redo = new List<List<TileEdit>>();
        internal int UndoCount => _undo.Count;
        internal int RedoCount => _redo.Count;
        internal void Commit(IEnumerable<PaintBuffer> buffers)
        {
            var edits = new List<TileEdit>(); foreach (var b in buffers) edits.AddRange(b.Commit());
            if (edits.Count == 0) return;
            _redo.Clear(); _undo.Add(edits);
            // Always retain the most recent action, including a whole-character reset.
            while (_undo.Count > 1 && (_undo.Count > 32 || Bytes() > 64L * 1024 * 1024)) _undo.RemoveAt(0);
        }
        long Bytes() => _undo.Sum(a => (long)a.Count * PaintBuffer.Tile * PaintBuffer.Tile * 8);
        internal void Undo() => Move(_undo, _redo, false);
        internal void Redo() => Move(_redo, _undo, true);
        static void Move(List<List<TileEdit>> from, List<List<TileEdit>> to, bool after)
        {
            if (from.Count == 0) return; var last = from[from.Count - 1]; from.RemoveAt(from.Count - 1);
            foreach (var edit in last) edit.Buffer.Restore(edit.Tile, after ? edit.After : edit.Before); to.Add(last);
        }
        internal void Clear() { _undo.Clear(); _redo.Clear(); }
    }
    internal enum PaintTool { Marker, Spray }
    internal enum PaintSetting { Color, Width, Grain, Transparency, Flow, Feather, Variation }
    internal sealed class StickState
    {
        internal bool Menu;
        internal float X, Y;
        int _direction;
        internal int Edge(float x, float y)
        {
            X = x; Y = y;
            if (Math.Abs(x) < .25 && Math.Abs(y) < .25) { _direction = 0; return 0; }
            if (_direction != 0 || Math.Max(Math.Abs(x), Math.Abs(y)) < .55) return 0;
            _direction = Math.Abs(x) > Math.Abs(y) ? (x > 0 ? 1 : -1) : (y > 0 ? 2 : -2);
            return _direction;
        }
        internal bool Vertical => Math.Abs(_direction) == 2;
        internal bool Horizontal => Math.Abs(_direction) == 1;
        internal int MenuSelection => Math.Abs(X) >= .45f && Math.Abs(X) >= Math.Abs(Y) ? (X < 0 ? 0 : 1) : -1;
        internal void Reset() { Menu = false; X = Y = 0; _direction = 0; }
        internal void BlockUntilNeutral() { Menu = false; _direction = 3; }
    }
}
