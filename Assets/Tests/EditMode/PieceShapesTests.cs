using System.Linq;
using NUnit.Framework;
using Tetris.Core;

namespace Tetris.Tests
{
    public class PieceShapesTests
    {
        static string Cells(PieceType p, int rot) =>
            string.Join(" ", PieceShapes.Cells(p, rot).OrderBy(c => c.Y).ThenBy(c => c.X).Select(c => $"({c.X},{c.Y})"));

        // Same expectations as TestCellsRotation in ai-server/internal/tetris/tetris_test.go.
        // If these diverge, the x / rotation the AI returns and Unity's movement will be out of sync.
        [TestCase(PieceType.T, 0, "(1,0) (0,1) (1,1) (2,1)")]
        [TestCase(PieceType.T, 1, "(1,0) (1,1) (2,1) (1,2)")]
        [TestCase(PieceType.T, 2, "(0,1) (1,1) (2,1) (1,2)")]
        [TestCase(PieceType.T, 3, "(1,0) (0,1) (1,1) (1,2)")]
        [TestCase(PieceType.I, 0, "(0,1) (1,1) (2,1) (3,1)")]
        [TestCase(PieceType.I, 1, "(2,0) (2,1) (2,2) (2,3)")]
        [TestCase(PieceType.L, 1, "(1,0) (1,1) (1,2) (2,2)")]
        [TestCase(PieceType.J, 1, "(1,0) (2,0) (1,1) (1,2)")]
        [TestCase(PieceType.S, 1, "(1,0) (1,1) (2,1) (2,2)")]
        [TestCase(PieceType.Z, 1, "(2,0) (1,1) (2,1) (1,2)")]
        public void CellsMatchAiServerContract(PieceType p, int rot, string expected)
        {
            Assert.AreEqual(expected, Cells(p, rot));
        }

        [Test]
        public void RotationIsCyclic()
        {
            foreach (PieceType p in System.Enum.GetValues(typeof(PieceType)))
            {
                Assert.AreEqual(Cells(p, 0), Cells(p, 4), p.ToString());
                Assert.AreEqual(Cells(p, 3), Cells(p, -1), p.ToString());
            }
        }
    }
}
