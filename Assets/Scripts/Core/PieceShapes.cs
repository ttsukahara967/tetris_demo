using System;

namespace Tetris.Core
{
    public enum PieceType { I = 0, O, T, S, Z, J, L }

    public readonly struct Cell
    {
        public readonly int X;
        public readonly int Y;

        public Cell(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    /// <summary>
    /// Piece shapes. The coordinate system is identical to the Go AI server (ai-server/internal/tetris/piece.go):
    /// each shape is a set of cells in a size x size box; rot 0..3 is the number of clockwise rotations (SRS).
    /// </summary>
    public static class PieceShapes
    {
        static readonly int[] Sizes = { 4, 2, 3, 3, 3, 3, 3 };

        static readonly Cell[][] BaseCells =
        {
            /* I */ new[] { new Cell(0, 1), new Cell(1, 1), new Cell(2, 1), new Cell(3, 1) },
            /* O */ new[] { new Cell(0, 0), new Cell(1, 0), new Cell(0, 1), new Cell(1, 1) },
            /* T */ new[] { new Cell(1, 0), new Cell(0, 1), new Cell(1, 1), new Cell(2, 1) },
            /* S */ new[] { new Cell(1, 0), new Cell(2, 0), new Cell(0, 1), new Cell(1, 1) },
            /* Z */ new[] { new Cell(0, 0), new Cell(1, 0), new Cell(1, 1), new Cell(2, 1) },
            /* J */ new[] { new Cell(0, 0), new Cell(0, 1), new Cell(1, 1), new Cell(2, 1) },
            /* L */ new[] { new Cell(2, 0), new Cell(0, 1), new Cell(1, 1), new Cell(2, 1) },
        };

        static readonly Cell[][][] Shapes = Build();

        static Cell[][][] Build()
        {
            var all = new Cell[BaseCells.Length][][];
            for (int p = 0; p < BaseCells.Length; p++)
            {
                int n = Sizes[p];
                var cur = (Cell[])BaseCells[p].Clone();
                all[p] = new Cell[4][];
                for (int r = 0; r < 4; r++)
                {
                    all[p][r] = (Cell[])cur.Clone();
                    for (int i = 0; i < cur.Length; i++)
                    {
                        // Rotate 90° clockwise: (x, y) -> (n-1-y, x)
                        cur[i] = new Cell(n - 1 - cur[i].Y, cur[i].X);
                    }
                }
            }
            return all;
        }

        public static int Size(PieceType p) => Sizes[(int)p];

        public static Cell[] Cells(PieceType p, int rot) => Shapes[(int)p][((rot % 4) + 4) % 4];

        public static void Bounds(PieceType p, int rot, out int minX, out int maxX, out int minY, out int maxY)
        {
            var cells = Cells(p, rot);
            minX = maxX = cells[0].X;
            minY = maxY = cells[0].Y;
            for (int i = 1; i < cells.Length; i++)
            {
                minX = Math.Min(minX, cells[i].X);
                maxX = Math.Max(maxX, cells[i].X);
                minY = Math.Min(minY, cells[i].Y);
                maxY = Math.Max(maxY, cells[i].Y);
            }
        }

        public static string Name(PieceType p) => p.ToString();
    }
}
