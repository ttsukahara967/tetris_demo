using NUnit.Framework;
using Tetris.Core;

namespace Tetris.Tests
{
    public class BoardTests
    {
        [Test]
        public void CollidesWithWallsAndFloor()
        {
            var b = new Board();
            Assert.IsTrue(b.Collides(PieceType.I, 0, -1, 5), "left wall");
            Assert.IsTrue(b.Collides(PieceType.I, 0, 7, 5), "right wall");
            Assert.IsTrue(b.Collides(PieceType.O, 0, 4, 19), "floor");
            Assert.IsFalse(b.Collides(PieceType.O, 0, 4, -1), "above the top is empty");
        }

        [Test]
        public void ClearLinesRemovesFullRowAndCompactsAbove()
        {
            var b = new Board();
            for (int c = 0; c < 6; c++) b.Set(19, c, 1);
            b.Set(18, 0, 3);
            Assert.IsFalse(b.Collides(PieceType.I, 0, 6, 18)); // A horizontal I goes into row oy+1
            b.Place(PieceType.I, 0, 6, 18);

            Assert.AreEqual(1, b.ClearLines());
            Assert.IsTrue(b.IsFilled(19, 0));
            Assert.IsFalse(b.IsFilled(18, 0));
            Assert.AreEqual(3, b.Get(19, 0));
        }

        [Test]
        public void AddGarbageRaisesBoardAndLeavesHole()
        {
            var b = new Board();
            b.Set(19, 5, 2);
            Assert.IsTrue(b.AddGarbage(2, 3));
            Assert.IsTrue(b.IsFilled(17, 5), "existing block is raised");
            Assert.IsFalse(b.IsFilled(19, 3), "hole");
            Assert.IsTrue(b.IsFilled(19, 4));
            Assert.IsTrue(b.IsFilled(18, 0));
        }

        [Test]
        public void AddGarbageReportsOverflow()
        {
            var b = new Board();
            b.Set(0, 0, 1);
            Assert.IsFalse(b.AddGarbage(1, 0));
        }
    }
}
