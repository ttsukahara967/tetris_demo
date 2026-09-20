using System;

namespace Tetris.Core
{
    /// <summary>
    /// The 10x20 board. Row 0 is the top row, col 0 is the left column.
    /// Cell values: 0 = empty, 1..7 = piece (PieceType + 1), 8 = garbage block.
    /// </summary>
    public sealed class Board
    {
        public const int Width = 10;
        public const int Height = 20;
        public const byte GarbageValue = 8;

        readonly byte[] cells = new byte[Width * Height];

        public byte Get(int row, int col) => cells[row * Width + col];

        public bool IsFilled(int row, int col) => cells[row * Width + col] != 0;

        public void Set(int row, int col, byte value) => cells[row * Width + col] = value;

        public void Clear() => Array.Clear(cells, 0, cells.Length);

        /// <summary>Whether placing the piece with its box origin at (ox, oy) hits a wall, the floor, or an existing block. Cells above the board (row &lt; 0) count as empty.</summary>
        public bool Collides(PieceType p, int rot, int ox, int oy)
        {
            foreach (var c in PieceShapes.Cells(p, rot))
            {
                int x = ox + c.X, y = oy + c.Y;
                if (x < 0 || x >= Width || y >= Height) return true;
                if (y >= 0 && cells[y * Width + x] != 0) return true;
            }
            return false;
        }

        /// <summary>Locks the piece into the board. Returns true (lock-out) if any cell sticks out above the board.</summary>
        public bool Place(PieceType p, int rot, int ox, int oy)
        {
            bool aboveTop = false;
            foreach (var c in PieceShapes.Cells(p, rot))
            {
                int x = ox + c.X, y = oy + c.Y;
                if (y < 0) { aboveTop = true; continue; }
                cells[y * Width + x] = (byte)((int)p + 1);
            }
            return aboveTop;
        }

        /// <summary>Removes full rows, compacts the rows above, and returns the number of rows cleared.</summary>
        public int ClearLines()
        {
            int cleared = 0;
            int dst = Height - 1;
            for (int src = Height - 1; src >= 0; src--)
            {
                if (RowFull(src)) { cleared++; continue; }
                if (dst != src) Array.Copy(cells, src * Width, cells, dst * Width, Width);
                dst--;
            }
            for (; dst >= 0; dst--) Array.Clear(cells, dst * Width, Width);
            return cleared;
        }

        /// <summary>Inserts count garbage rows at the bottom (empty only at holeCol) and lifts everything up. Returns false if a block is pushed off the board.</summary>
        public bool AddGarbage(int count, int holeCol)
        {
            if (count <= 0) return true;
            count = Math.Min(count, Height);

            bool overflow = false;
            for (int i = 0; i < count * Width; i++)
            {
                if (cells[i] != 0) { overflow = true; break; }
            }

            Array.Copy(cells, count * Width, cells, 0, (Height - count) * Width);
            for (int r = Height - count; r < Height; r++)
            {
                for (int c = 0; c < Width; c++)
                    cells[r * Width + c] = c == holeCol ? (byte)0 : GarbageValue;
            }
            return !overflow;
        }

        public bool RowFull(int row)
        {
            for (int c = 0; c < Width; c++)
                if (cells[row * Width + c] == 0) return false;
            return true;
        }
    }
}
