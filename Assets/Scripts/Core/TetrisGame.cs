using System;
using System.Collections.Generic;

namespace Tetris.Core
{
    /// <summary>
    /// The rules for one player: falling, rotation, locking, line clears, score, and garbage lines.
    /// Independent of UnityEngine. Time advances via Update(dt); input is applied by calling methods.
    /// </summary>
    public sealed class TetrisGame
    {
        public const int NextCount = 5;
        public const float LockDelay = 0.5f;
        public const int MaxLockResets = 15;
        const int MaxGarbagePerLock = 8;

        static readonly int[] LineScores = { 0, 100, 300, 500, 800 };
        static readonly int[] AttackLines = { 0, 0, 1, 2, 4 };
        static readonly (int dx, int dy)[] Kicks =
        {
            (0, 0), (-1, 0), (1, 0), (-2, 0), (2, 0), (0, -1), (-1, -1), (1, -1),
        };

        readonly BagRandomizer bag;
        readonly Random garbageRng;
        readonly Queue<PieceType> queue = new Queue<PieceType>();
        float gravityTimer;
        float lockTimer;
        int lockResets;

        public Board Board { get; } = new Board();

        public PieceType Current { get; private set; }
        public int Rotation { get; private set; }
        /// <summary>Box origin (top-left) of the current piece.</summary>
        public int PosX { get; private set; }
        public int PosY { get; private set; }

        public int Score { get; private set; }
        public int Lines { get; private set; }
        public int Level => Lines / 10 + 1;
        public bool IsGameOver { get; private set; }

        /// <summary>Increments every time a piece spawns. Used to tell whether a CPU response is stale.</summary>
        public long PieceSerial { get; private set; }

        /// <summary>Garbage lines received from the opponent that are not yet applied to the board.</summary>
        public int PendingGarbage { get; private set; }

        public event Action<int> LinesCleared;
        /// <summary>Garbage lines to send to the opponent, after cancelling against pending garbage.</summary>
        public event Action<int> GarbageSent;
        public event Action GameOver;

        public TetrisGame(int seed)
        {
            bag = new BagRandomizer(seed);
            garbageRng = new Random(seed ^ 0x5eed);
            for (int i = 0; i < NextCount + 1; i++) queue.Enqueue(bag.Next());
            Spawn();
        }

        public PieceType PeekNext(int i = 0)
        {
            if (i < 0 || i >= queue.Count) throw new ArgumentOutOfRangeException(nameof(i));
            int n = 0;
            foreach (var p in queue)
            {
                if (n++ == i) return p;
            }
            throw new InvalidOperationException();
        }

        /// <summary>Seconds per one-row fall (guideline formula).</summary>
        public float GravityInterval => Math.Max(0.02f, (float)Math.Pow(0.8 - (Level - 1) * 0.007, Level - 1));

        public bool IsGrounded => Board.Collides(Current, Rotation, PosX, PosY + 1);

        /// <summary>Leftmost column of the piece after rotation (same definition as x in the AI server).</summary>
        public int LeftmostX
        {
            get
            {
                PieceShapes.Bounds(Current, Rotation, out int minX, out _, out _, out _);
                return PosX + minX;
            }
        }

        public void Update(float dt)
        {
            if (IsGameOver) return;

            if (IsGrounded)
            {
                gravityTimer = 0;
                lockTimer += dt;
                if (lockTimer >= LockDelay) LockAndSpawn();
                return;
            }

            lockTimer = 0;
            gravityTimer += dt;
            float interval = GravityInterval;
            while (gravityTimer >= interval && !IsGrounded)
            {
                gravityTimer -= interval;
                PosY++;
            }
        }

        public bool MoveLeft() => TryMove(-1, 0);

        public bool MoveRight() => TryMove(1, 0);

        /// <summary>Moves down one row via soft drop (1 point per row).</summary>
        public bool SoftDrop()
        {
            if (IsGameOver || IsGrounded) return false;
            PosY++;
            Score++;
            gravityTimer = 0;
            return true;
        }

        public void HardDrop()
        {
            if (IsGameOver) return;
            int dropped = 0;
            while (!IsGrounded)
            {
                PosY++;
                dropped++;
            }
            Score += dropped * 2;
            LockAndSpawn();
        }

        /// <summary>dir=+1 rotates clockwise, -1 counter-clockwise. Tries wall kicks.</summary>
        public bool Rotate(int dir)
        {
            if (IsGameOver) return false;
            int newRot = ((Rotation + dir) % 4 + 4) % 4;
            foreach (var (dx, dy) in Kicks)
            {
                if (Board.Collides(Current, newRot, PosX + dx, PosY + dy)) continue;
                Rotation = newRot;
                PosX += dx;
                PosY += dy;
                OnMoved();
                return true;
            }
            return false;
        }

        /// <summary>Y of the landing position (for the ghost piece).</summary>
        public int GhostY()
        {
            int y = PosY;
            while (!Board.Collides(Current, Rotation, PosX, y + 1)) y++;
            return y;
        }

        /// <summary>
        /// For the CPU. Puts the piece back at the spawn position (top row, near the center) with the given rotation.
        /// Recreates the state the AI server assumes ("dropped from the top row"). Returns false if it does not fit.
        /// </summary>
        public bool TryAlignRotation(int rotation)
        {
            if (IsGameOver) return false;
            int rot = ((rotation % 4) + 4) % 4;
            PieceShapes.Bounds(Current, rot, out _, out _, out int minY, out _);
            int ox = (Board.Width - PieceShapes.Size(Current)) / 2;
            int oy = -minY;
            if (Board.Collides(Current, rot, ox, oy)) return false;
            Rotation = rot;
            PosX = ox;
            PosY = oy;
            return true;
        }

        /// <summary>Receives garbage lines. They are applied to the board on the next lock (if no lines are cleared).</summary>
        public void ReceiveGarbage(int lines)
        {
            if (lines > 0) PendingGarbage += lines;
        }

        bool TryMove(int dx, int dy)
        {
            if (IsGameOver || Board.Collides(Current, Rotation, PosX + dx, PosY + dy)) return false;
            PosX += dx;
            PosY += dy;
            OnMoved();
            return true;
        }

        void OnMoved()
        {
            // Extend the lock delay when the piece is moved while grounded (limited number of times).
            if (lockTimer > 0 && lockResets < MaxLockResets)
            {
                lockTimer = 0;
                lockResets++;
            }
        }

        void LockAndSpawn()
        {
            bool lockOut = Board.Place(Current, Rotation, PosX, PosY);
            int cleared = Board.ClearLines();

            if (cleared > 0)
            {
                Score += LineScores[Math.Min(cleared, 4)] * Level;
                Lines += cleared;
                LinesCleared?.Invoke(cleared);

                int attack = AttackLines[Math.Min(cleared, 4)];
                int cancel = Math.Min(attack, PendingGarbage);
                PendingGarbage -= cancel;
                attack -= cancel;
                if (attack > 0) GarbageSent?.Invoke(attack);
            }
            else if (PendingGarbage > 0)
            {
                int n = Math.Min(PendingGarbage, MaxGarbagePerLock);
                PendingGarbage -= n;
                if (!Board.AddGarbage(n, garbageRng.Next(Board.Width))) lockOut = true;
            }

            if (lockOut)
            {
                EndGame();
                return;
            }
            Spawn();
        }

        void Spawn()
        {
            Current = queue.Dequeue();
            queue.Enqueue(bag.Next());
            Rotation = 0;
            PieceShapes.Bounds(Current, 0, out _, out _, out int minY, out _);
            PosX = (Board.Width - PieceShapes.Size(Current)) / 2;
            PosY = -minY;
            gravityTimer = 0;
            lockTimer = 0;
            lockResets = 0;
            PieceSerial++;
            if (Board.Collides(Current, Rotation, PosX, PosY)) EndGame();
        }

        void EndGame()
        {
            if (IsGameOver) return;
            IsGameOver = true;
            GameOver?.Invoke();
        }
    }
}
