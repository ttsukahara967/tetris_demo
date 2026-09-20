using System.Reflection;
using NUnit.Framework;
using Tetris.Core;

namespace Tetris.Tests
{
    public class TetrisGameTests
    {
        /// <summary>Drops the current piece with the given type, rotation, and leftmost x (swaps Current for testing).</summary>
        static void Play(TetrisGame g, PieceType type, int rot, int x)
        {
            typeof(TetrisGame).GetProperty("Current").SetValue(g, type);
            Assert.IsTrue(g.TryAlignRotation(rot));
            while (g.LeftmostX > x) Assert.IsTrue(g.MoveLeft());
            while (g.LeftmostX < x) Assert.IsTrue(g.MoveRight());
            g.HardDrop();
        }

        static bool AnyFilled(Board b)
        {
            for (int r = 0; r < Board.Height; r++)
                for (int c = 0; c < Board.Width; c++)
                    if (b.IsFilled(r, c)) return true;
            return false;
        }

        [Test]
        public void MovesAndStopsAtWall()
        {
            var g = new TetrisGame(1);
            int x0 = g.LeftmostX;
            Assert.IsTrue(g.MoveLeft());
            Assert.AreEqual(x0 - 1, g.LeftmostX);
            while (g.MoveLeft()) { }
            Assert.AreEqual(0, g.LeftmostX);
        }

        [Test]
        public void SoftDropScoresOnePerRow()
        {
            var g = new TetrisGame(2);
            g.SoftDrop();
            Assert.AreEqual(1, g.Score);
        }

        [Test]
        public void HardDropLocksAndSpawnsNextPiece()
        {
            var g = new TetrisGame(3);
            long serial = g.PieceSerial;
            g.HardDrop();
            Assert.AreEqual(serial + 1, g.PieceSerial);
            Assert.Greater(g.Score, 0);
            Assert.IsTrue(AnyFilled(g.Board));
        }

        [Test]
        public void GravityEventuallyLocksAnIdlePiece()
        {
            var g = new TetrisGame(4);
            long serial = g.PieceSerial;
            for (int i = 0; i < 1000 && g.PieceSerial == serial; i++) g.Update(0.05f);
            Assert.Greater(g.PieceSerial, serial);
        }

        [Test]
        public void StackingToTheTopEndsTheGame()
        {
            var g = new TetrisGame(5);
            bool raised = false;
            g.GameOver += () => raised = true;
            for (int i = 0; i < 200 && !g.IsGameOver; i++) g.HardDrop();
            Assert.IsTrue(g.IsGameOver);
            Assert.IsTrue(raised);
            Assert.IsFalse(g.MoveLeft());
            Assert.IsFalse(g.Rotate(1));
        }

        [Test]
        public void ClearingALineUpdatesScoreLinesAndRaisesEvent()
        {
            var g = new TetrisGame(7);
            int cleared = 0;
            g.LinesCleared += n => cleared += n;
            for (int c = 0; c < Board.Width; c++)
                if (c < 3 || c > 6) g.Board.Set(19, c, 1);

            Play(g, PieceType.I, 0, 3);

            Assert.AreEqual(1, cleared);
            Assert.AreEqual(1, g.Lines);
        }

        [Test]
        public void PendingGarbageIsAppliedOnLockWithoutClear()
        {
            var g = new TetrisGame(8);
            g.ReceiveGarbage(2);
            Assert.AreEqual(2, g.PendingGarbage);
            g.HardDrop();
            Assert.AreEqual(0, g.PendingGarbage);
            Assert.IsTrue(g.Board.IsFilled(19, 0) || g.Board.IsFilled(19, 1));
        }

        [Test]
        public void TetrisSendsFourGarbageLines()
        {
            var g = new TetrisGame(9);
            int sent = 0;
            g.GarbageSent += n => sent += n;
            for (int r = 16; r < 20; r++)
                for (int c = 1; c < Board.Width; c++) g.Board.Set(r, c, 1);

            Play(g, PieceType.I, 1, 0);

            Assert.AreEqual(4, sent);
        }

        [Test]
        public void SentGarbageIsCancelledByPendingGarbage()
        {
            var g = new TetrisGame(10);
            int sent = 0;
            g.GarbageSent += n => sent += n;
            g.ReceiveGarbage(1);
            for (int r = 17; r < 20; r++)
                for (int c = 1; c < Board.Width; c++) g.Board.Set(r, c, 1);

            Play(g, PieceType.I, 1, 0); // clearing 3 lines = send 2, minus 1 cancelled

            Assert.AreEqual(1, sent);
            Assert.AreEqual(0, g.PendingGarbage);
        }

        [Test]
        public void TryAlignRotationPutsPieceAtTopRow()
        {
            var g = new TetrisGame(11);
            Assert.IsTrue(g.TryAlignRotation(1));
            PieceShapes.Bounds(g.Current, g.Rotation, out _, out _, out int minY, out _);
            Assert.AreEqual(0, g.PosY + minY);
        }
    }
}
